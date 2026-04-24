using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Appointments.Dto;
using PropelIQ.Modules.Scheduling.Domain.Entities;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Appointments;

/// <summary>
/// Orchestrates paginated appointment history retrieval and PDF export.
///
/// AC-1: Returns appointments sorted date descending.
/// AC-2: Status filter; 500 ms p95 ensured by composite index on (PatientId, ScheduledAt DESC, Status).
/// AC-3: Date-range filter applied in repository.
/// AC-4: PDF export streams all filtered records via <see cref="IAppointmentHistoryRepository.StreamFilteredAsync"/>
///       — the complete result set is included regardless of pagination settings.
/// Edge case: empty history returns 200 with Items=[] and TotalCount=0.
/// </summary>
public sealed class AppointmentHistoryService
{
    private readonly IAppointmentHistoryRepository _historyRepo;
    private readonly AppointmentHistoryPdfGenerator _pdfGenerator;
    private readonly ILogger<AppointmentHistoryService> _logger;

    public AppointmentHistoryService(
        IAppointmentHistoryRepository historyRepo,
        AppointmentHistoryPdfGenerator pdfGenerator,
        ILogger<AppointmentHistoryService> logger)
    {
        _historyRepo  = historyRepo;
        _pdfGenerator = pdfGenerator;
        _logger       = logger;
    }

    /// <summary>
    /// Returns a paginated, filtered appointment history for the given patient.
    /// AC-1 / AC-2 / AC-3.
    /// </summary>
    public async Task<AppointmentHistoryResponse> GetHistoryAsync(
        Guid patientId,
        AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Fetching appointment history for patient {PatientId} (page={Page}, pageSize={PageSize}, status={Status})",
            patientId, filter.Page, filter.PageSize, filter.Status ?? "any");

        var (items, totalCount) = await _historyRepo.GetFilteredAsync(patientId, filter, ct);

        return new AppointmentHistoryResponse
        {
            Items      = items.Select(MapToItem).ToList(),
            TotalCount = totalCount,
            Page       = filter.Page,
            PageSize   = filter.PageSize,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)filter.PageSize),
        };
    }

    /// <summary>
    /// Generates a PDF containing all filtered appointments for the given patient.
    /// AC-4: streams complete result set — ignores pagination.
    /// </summary>
    public async Task<byte[]> ExportPdfAsync(
        Guid patientId,
        AppointmentHistoryFilter filter,
        CancellationToken ct)
    {
        // Use a page-1 / max-records filter when streaming to PDF.
        var exportFilter = filter with { Page = 1, PageSize = int.MaxValue };

        _logger.LogInformation(
            "Exporting appointment history PDF for patient {PatientId} (status={Status})",
            patientId, exportFilter.Status ?? "any");

        var appointments = new List<AppointmentHistoryItem>();

        await foreach (var apt in _historyRepo.StreamFilteredAsync(patientId, exportFilter, ct))
            appointments.Add(MapToItem(apt));

        _logger.LogInformation(
            "Generating appointment history PDF for patient {PatientId} ({Count} records)",
            patientId, appointments.Count);

        return _pdfGenerator.Generate(appointments, filter);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static AppointmentHistoryItem MapToItem(Appointment apt) =>
        new()
        {
            Id               = apt.Id,
            ScheduledAt      = apt.ScheduledAt,
            DurationMinutes  = apt.DurationMinutes,
            AppointmentType  = apt.AppointmentType,
            Status           = apt.Status,
            ProviderName     = apt.ProviderName,
            Location         = apt.Location,
            ConfirmationCode = apt.ConfirmationCode ?? string.Empty,
        };
}
