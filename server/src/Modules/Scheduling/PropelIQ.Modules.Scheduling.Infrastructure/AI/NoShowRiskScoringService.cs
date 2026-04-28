using System.Text.Json;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.AI;
using PropelIQ.Modules.Scheduling.Application.AI.Models;
using PropelIQ.Modules.Scheduling.Application.AI.Prompts;
using PropelIQ.Modules.Scheduling.Application.AI.Validators;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.SharedKernel.AiGateway;
using PropelIQ.SharedKernel.AiGateway.Models;

namespace PropelIQ.Modules.Scheduling.Infrastructure.AI;

/// <summary>
/// Evaluates no-show risk for an appointment through the AI gateway (TR-008).
///
/// AC-1: Constructs a structured prompt from PII-free patient history features
///       and returns a risk label with explainable feature contributions.
/// AC-4: Caches the result on the Appointment entity with a RiskScoredAt timestamp.
///       Staleness (24h TTL) is checked before making a gateway call (edge case 2).
///
/// Edge case 1: When the gateway is unavailable or returns an invalid response,
///              RiskLevel = Unknown is persisted and returned — no false indicators.
/// AIR-009: No PII reaches the prompt — only aggregated counts and slot metadata.
/// AIR-011: All prompt calls, responses, and confidence values are logged for
///          7-year retention compliance.
/// AIR-006: The end-to-end scoring must complete within 2.5 s p95; the gateway
///          client's Polly timeout (AiGatewayOptions.TimeoutSeconds) enforces this.
/// </summary>
public sealed class NoShowRiskScoringService : INoShowRiskScoringService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IAiGatewayClient _gateway;
    private readonly IPatientHistoryFeatureExtractor _extractor;
    private readonly IBookingRepository _bookingRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NoShowRiskScoringService> _logger;

    public NoShowRiskScoringService(
        IAiGatewayClient gateway,
        IPatientHistoryFeatureExtractor extractor,
        IBookingRepository bookingRepository,
        TimeProvider timeProvider,
        ILogger<NoShowRiskScoringService> logger)
    {
        _gateway = gateway;
        _extractor = extractor;
        _bookingRepository = bookingRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NoShowRiskResult> ScoreAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        var appointment = await _bookingRepository
            .GetAppointmentAsync(appointmentId, ct)
            ?? throw new InvalidOperationException(
                $"Appointment {appointmentId} not found.");

        var now = _timeProvider.GetUtcNow();

        // Edge case 2: return cached score when it is still within the 24-hour TTL.
        if (appointment.RiskScoredAt.HasValue
            && appointment.RiskLevel is not null
            && (now - appointment.RiskScoredAt.Value) < NoShowRiskDefaults.StalenessThreshold)
        {
            _logger.LogDebug(
                "Returning cached risk score for appointment {Id} (scored at {ScoredAt})",
                appointmentId, appointment.RiskScoredAt.Value);

            return ParseCachedScore(appointment);
        }

        // AIR-009: extract PII-free aggregated features for the prompt.
        var features = await _extractor.ExtractAsync(appointment.PatientId, ct);

        // Edge case 1: skip the gateway call when the circuit breaker is already open.
        if (_gateway.IsCircuitBreakerOpen)
        {
            _logger.LogWarning(
                "AI gateway circuit breaker is open — persisting Unknown risk " +
                "for appointment {Id}",
                appointmentId);

            return await PersistAndReturnAsync(
                appointmentId, NoShowRiskDefaults.Unknown, now, ct);
        }

        var request = new ChatCompletionRequest
        {
            Model = NoShowRiskPrompt.ModelAlias,
            Temperature = 0.2,  // low temperature for deterministic risk classification
            MaxTokens = 512,
            Messages =
            [
                new ChatMessage
                {
                    Role = "system",
                    Content = NoShowRiskPrompt.SystemPrompt
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = NoShowRiskPrompt.Build(features)
                },
            ],
        };

        try
        {
            // AIR-006: 2.5 s p95 budget is enforced by the Polly timeout on the
            // LiteLlmGatewayClient's HttpClient handler.
            var response = await _gateway.GetCompletionAsync(request, ct);

            if (response is null)
            {
                // Gateway returned null — circuit open or 401.
                _logger.LogWarning(
                    "AI gateway returned null for no-show risk scoring of " +
                    "appointment {Id} — persisting Unknown",
                    appointmentId);

                return await PersistAndReturnAsync(
                    appointmentId, NoShowRiskDefaults.Unknown, now, ct);
            }

            var content = response.Choices.Count > 0
                ? response.Choices[0].Message.Content
                : string.Empty;

            // AIR-011: log prompt response length and token usage for audit.
            _logger.LogInformation(
                "NoShowRisk scored for appointment {AppointmentId}: " +
                "response_length={Length}, prompt_tokens={PromptTokens}, " +
                "completion_tokens={CompletionTokens}",
                appointmentId,
                content.Length,
                response.Usage.PromptTokens,
                response.Usage.CompletionTokens);

            // AIR-008: validate schema before accepting the model output.
            if (!NoShowRiskResponseValidator.TryParse(content, out var result)
                || result is null)
            {
                _logger.LogWarning(
                    "No-show risk model returned an invalid response schema " +
                    "for appointment {Id} — persisting Unknown. Raw: {Raw}",
                    appointmentId,
                    content.Length > 200 ? content[..200] : content);

                return await PersistAndReturnAsync(
                    appointmentId, NoShowRiskDefaults.Unknown, now, ct);
            }

            return await PersistAndReturnAsync(appointmentId, result, now, ct);
        }
        catch (Exception ex)
        {
            // Edge case 1: any unexpected gateway failure — fall back to Unknown.
            _logger.LogWarning(ex,
                "No-show risk model unavailable for appointment {Id} — " +
                "persisting Unknown",
                appointmentId);

            return await PersistAndReturnAsync(
                appointmentId, NoShowRiskDefaults.Unknown, now, ct);
        }
    }

    private async Task<NoShowRiskResult> PersistAndReturnAsync(
        Guid appointmentId,
        NoShowRiskResult result,
        DateTimeOffset scoredAt,
        CancellationToken ct)
    {
        var featuresJson = JsonSerializer.Serialize(result.Features, JsonOpts);

        await _bookingRepository.UpdateRiskScoreAsync(
            appointmentId,
            result.RiskLevel,
            result.Confidence,
            featuresJson,
            scoredAt,
            ct);

        return result;
    }

    private static NoShowRiskResult ParseCachedScore(
        PropelIQ.Modules.Scheduling.Domain.Entities.Appointment appointment)
    {
        var features = string.IsNullOrEmpty(appointment.RiskFeatures)
            ? Array.Empty<RiskFeatureContribution>()
            : (IReadOnlyList<RiskFeatureContribution>)
              (JsonSerializer.Deserialize<List<RiskFeatureContribution>>(
                   appointment.RiskFeatures, new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true
                   }) ?? []);

        return new NoShowRiskResult(
            appointment.RiskLevel!,
            appointment.RiskConfidence ?? 0.0,
            features);
    }
}
