using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.SharedServices.Application.Audit;
using PropelIQ.Modules.SharedServices.Application.Disclosure;
using PropelIQ.Modules.SharedServices.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;
using PropelIQ.SharedKernel.Notifications;

namespace PropelIQ.Modules.SharedServices.Infrastructure.Disclosure;

/// <summary>
/// Implements <see cref="IDisclosureService"/> — orchestrates patient disclosure
/// request submission, compilation polling, staff review, delivery, and download
/// (US_057, AC-2, AC-3, AC-4, edge cases 1 + 2).
///
/// <para>
/// Download tokens are HMAC-SHA256 signed with the key at
/// <c>Disclosure:DownloadTokenSecret</c>. Format:
/// <c>{reportId}|{requestId}|{unixExpiry}|{hexSignature}</c>
/// </para>
/// </summary>
public sealed class DisclosureService : IDisclosureService
{
    private const int DownloadLinkHours = 48;

    private readonly AppDbContext _db;
    private readonly IAuditRecordService _audit;
    private readonly INotificationSender _notifications;
    private readonly IConfiguration _config;
    private readonly ILogger<DisclosureService> _logger;

    public DisclosureService(
        AppDbContext db,
        IAuditRecordService audit,
        INotificationSender notifications,
        IConfiguration config,
        ILogger<DisclosureService> logger)
    {
        _db            = db;
        _audit         = audit;
        _notifications = notifications;
        _config        = config;
        _logger        = logger;
    }

    // ── AC-2: Submit ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Guid> SubmitAsync(
        Guid patientId,
        DateTimeOffset fromDateUtc,
        DateTimeOffset toDateUtc,
        CancellationToken ct = default)
    {
        var request = new DisclosureRequest
        {
            PatientId   = patientId,
            FromDateUtc = fromDateUtc,
            ToDateUtc   = toDateUtc,
        };

        _db.DisclosureRequests.Add(request);
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync(new AuditEvent
        {
            UserId     = patientId,
            EventType  = "DisclosureRequested",
            EntityType = nameof(DisclosureRequest),
            EntityId   = request.Id,
            Details    = new Dictionary<string, object>
            {
                ["fromDateUtc"] = fromDateUtc.ToString("O"),
                ["toDateUtc"]   = toDateUtc.ToString("O"),
            },
        }, ct);

        _logger.LogInformation(
            "Disclosure request {RequestId} submitted by patient {PatientId} for range {From}–{To}.",
            request.Id, patientId, fromDateUtc, toDateUtc);

        return request.Id;
    }

    // ── Patient read endpoints ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<DisclosureRequestDto?> GetByIdForPatientAsync(
        Guid patientId,
        Guid requestId,
        CancellationToken ct = default)
    {
        var entity = await _db.DisclosureRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requestId && r.PatientId == patientId, ct);

        return entity is null ? null : MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DisclosureRequestDto>> ListForPatientAsync(
        Guid patientId,
        CancellationToken ct = default)
    {
        var entities = await _db.DisclosureRequests
            .AsNoTracking()
            .Where(r => r.PatientId == patientId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ReportDownloadResult?> GetReportForDownloadAsync(
        Guid patientId,
        Guid requestId,
        string token,
        CancellationToken ct = default)
    {
        var request = await _db.DisclosureRequests
            .AsNoTracking()
            .Include(r => r.Report)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.PatientId == patientId, ct);

        if (request is null || request.Report is null)
            return null;

        var report = request.Report;

        // Validate the stored token (immutable after generation; token arg must match).
        if (string.IsNullOrEmpty(report.DownloadToken) || report.DownloadToken != token)
            return null;

        // Edge case 1: token expired after 48 hours.
        if (report.DownloadExpiresAt.HasValue && report.DownloadExpiresAt.Value < DateTimeOffset.UtcNow)
            return new ReportDownloadResult(IsExpired: true, Content: []);

        var bytes = Encoding.UTF8.GetBytes(report.ReportJson);
        return new ReportDownloadResult(IsExpired: false, Content: bytes);
    }

    // ── Staff review endpoints ────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<DisclosureRequestDto>> ListForReviewAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.DisclosureRequests.AsNoTracking();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DisclosureStatus>(status, out var statusEnum))
            query = query.Where(r => r.Status == statusEnum);

        var entities = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return entities.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> ReviewAsync(
        Guid requestId,
        Guid reviewerId,
        bool approved,
        string? notes,
        CancellationToken ct = default)
    {
        var request = await _db.DisclosureRequests
            .Include(r => r.Report)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request is null) return false;

        request.ReviewedBy  = reviewerId;
        request.ReviewedAt  = DateTimeOffset.UtcNow;
        request.ReviewNotes = notes;

        if (!approved)
        {
            request.Transition(DisclosureStatus.Rejected);
            await _db.SaveChangesAsync(ct);

            await _audit.WriteAsync(new AuditEvent
            {
                UserId     = reviewerId,
                EventType  = "DisclosureRejected",
                EntityType = nameof(DisclosureRequest),
                EntityId   = requestId,
                Details    = new Dictionary<string, object> { ["notes"] = notes ?? string.Empty },
            }, ct);

            return true;
        }

        // Approved path: generate download token + send email.
        if (request.Report is null)
        {
            _logger.LogWarning(
                "Cannot approve disclosure request {RequestId}: report not yet compiled.", requestId);
            return false;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddHours(DownloadLinkHours);
        var token     = GenerateDownloadToken(request.Report.Id, requestId, expiresAt);

        request.Report.DownloadToken     = token;
        request.Report.DownloadExpiresAt = expiresAt;

        request.Transition(DisclosureStatus.Approved);
        await _db.SaveChangesAsync(ct);

        // Deliver via email (AC-3).
        await DeliverByEmailAsync(request, token, ct);

        request.DeliveredAt    = DateTimeOffset.UtcNow;
        request.DeliveryMethod = "Email";
        request.Transition(DisclosureStatus.Delivered);
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync(new AuditEvent
        {
            UserId     = reviewerId,
            EventType  = "DisclosureApproved",
            EntityType = nameof(DisclosureRequest),
            EntityId   = requestId,
            Details    = new Dictionary<string, object>
            {
                ["deliveredAt"] = request.DeliveredAt.Value.ToString("O"),
                ["expiresAt"]   = expiresAt.ToString("O"),
            },
        }, ct);

        return true;
    }

    /// <inheritdoc />
    public async Task<DisclosureReportDto?> GetReportForReviewAsync(
        Guid requestId,
        CancellationToken ct = default)
    {
        var report = await _db.DisclosureReports
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.DisclosureRequestId == requestId, ct);

        return report is null ? null : new DisclosureReportDto(
            report.Id,
            report.DisclosureRequestId,
            report.AccessEventCount,
            report.CreatedAt,
            report.ReportJson,
            report.DownloadToken is not null);
    }

    // ── HMAC token helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Produces a tamper-proof HMAC-SHA256 download token.
    /// Format: {reportId}|{requestId}|{unixExpiry}|{hexSignature}
    /// </summary>
    private string GenerateDownloadToken(
        Guid reportId,
        Guid requestId,
        DateTimeOffset expiresAt)
    {
        var payload = $"{reportId}|{requestId}|{expiresAt.ToUnixTimeSeconds()}";
        var key     = GetHmacKey();

        using var hmac = new HMACSHA256(key);
        var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return $"{payload}|{Convert.ToHexString(sig).ToLowerInvariant()}";
    }

    private byte[] GetHmacKey()
    {
        var secret = _config["Disclosure:DownloadTokenSecret"];
        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException(
                "Configuration key 'Disclosure:DownloadTokenSecret' is required but missing.");

        return Encoding.UTF8.GetBytes(secret);
    }

    // ── Email delivery ────────────────────────────────────────────────────────

    private async Task DeliverByEmailAsync(
        DisclosureRequest request,
        string token,
        CancellationToken ct)
    {
        var patient = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.PatientId, ct);

        if (patient is null)
        {
            _logger.LogWarning(
                "Cannot deliver disclosure {RequestId}: patient user {PatientId} not found.",
                request.Id, request.PatientId);
            return;
        }

        var apiBase     = _config["App:ApiBaseUrl"] ?? "https://api.propeliq.com";
        var downloadUrl = $"{apiBase}/api/v1/patients/me/disclosure-requests/{request.Id}/download?token={Uri.EscapeDataString(token)}";

        var html = $"""
            <p>Dear {patient.FirstName ?? "Patient"},</p>
            <p>Your data disclosure report for the period
               <strong>{request.FromDateUtc:yyyy-MM-dd}</strong> to
               <strong>{request.ToDateUtc:yyyy-MM-dd}</strong>
               has been reviewed and approved.</p>
            <p>You can securely download your report using the link below. The link is valid for 48 hours.</p>
            <p><a href="{downloadUrl}">Download Disclosure Report</a></p>
            <p>If you did not request this disclosure, please contact support immediately.</p>
            <p>— PropelIQ Compliance Team</p>
            """;

        await _notifications.SendEmailAsync(
            to:       patient.Email,
            subject:  "Your PropelIQ Data Disclosure Report Is Ready",
            htmlBody: html,
            ct:       ct);
    }

    // ── Projection helpers ────────────────────────────────────────────────────

    private static DisclosureRequestDto MapToDto(DisclosureRequest r) =>
        new(r.Id, r.PatientId, r.FromDateUtc, r.ToDateUtc, r.Status,
            r.CreatedAt, r.CompiledAt, r.ReviewedBy, r.ReviewedAt,
            r.ReviewNotes, r.DeliveredAt, r.DeliveryMethod, r.ReportId);
}
