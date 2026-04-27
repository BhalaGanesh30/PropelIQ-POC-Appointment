using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PropelIQ.Modules.Scheduling.Application.Abstractions;
using PropelIQ.Modules.Scheduling.Application.Intake.Dto;
using PropelIQ.Modules.Scheduling.Domain.Entities;
using PropelIQ.Modules.SharedServices.Infrastructure.Data;

namespace PropelIQ.Modules.Scheduling.Infrastructure.Intake;

/// <summary>
/// Orchestrates intake draft autosave, retrieval, and final submission.
/// Lives in Infrastructure to access both IIntakeDraftRepository and AppDbContext
/// without introducing a circular dependency from Application.
/// </summary>
public sealed class IntakeDraftService
{
    private readonly IIntakeDraftRepository _draftRepo;
    private readonly AppDbContext _context;
    private readonly ILogger<IntakeDraftService> _logger;

    public IntakeDraftService(
        IIntakeDraftRepository draftRepo,
        AppDbContext context,
        ILogger<IntakeDraftService> logger)
    {
        _draftRepo = draftRepo;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Autosave partial form data on each blur event (AC-2).
    /// Upserts the draft — creates on first call, updates on subsequent calls.
    /// Returns a timestamp for the client-side "Saved" indicator.
    /// </summary>
    public async Task<SaveDraftResponse> SaveDraftAsync(
        Guid patientId,
        SaveDraftRequest request,
        CancellationToken ct)
    {
        var draft = new IntakeDraft
        {
            PatientId = patientId,
            SlotId = request.SlotId,
            FormData = request.FormData ?? JsonDocument.Parse("{}"),
            AiPopulatedFields = request.AiPopulatedFields ?? [],
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
        };

        var saved = await _draftRepo.UpsertAsync(draft, ct);

        return new SaveDraftResponse
        {
            DraftId = saved.Id,
            SavedAt = saved.UpdatedAt,
        };
    }

    /// <summary>
    /// Retrieve the patient's saved draft for resume-from-where-left-off (AC-3).
    /// Returns null (mapped to 204) when no draft exists.
    /// </summary>
    public async Task<IntakeDraftResponse?> GetDraftAsync(
        Guid patientId,
        Guid? slotId,
        CancellationToken ct)
    {
        var draft = slotId.HasValue
            ? await _draftRepo.GetByPatientAndSlotAsync(patientId, slotId, ct)
            : await _draftRepo.GetLatestByPatientAsync(patientId, ct);

        if (draft is null) return null;

        return new IntakeDraftResponse
        {
            Id = draft.Id,
            SlotId = draft.SlotId,
            FormData = draft.FormData,
            AiPopulatedFields = draft.AiPopulatedFields,
            Status = draft.Status.ToString(),
            UpdatedAt = draft.UpdatedAt,
        };
    }

    /// <summary>
    /// Validate, finalize and attach intake to the appointment booking (AC-4).
    /// Transitions draft status to Submitted and creates an IntakeRecord.
    /// Throws <see cref="InvalidOperationException"/> when draft is not found / already submitted.
    /// Throws <see cref="UnauthorizedAccessException"/> when draft belongs to a different patient.
    /// </summary>
    public async Task<SubmitIntakeResponse> SubmitIntakeAsync(
        Guid patientId,
        SubmitIntakeRequest request,
        CancellationToken ct)
    {
        var draft = await _draftRepo.GetByIdAsync(request.DraftId, ct)
            ?? throw new InvalidOperationException(
                "Intake draft not found or has already been submitted.");

        if (draft.PatientId != patientId)
            throw new UnauthorizedAccessException(
                "Intake draft does not belong to the authenticated patient.");

        // Transition draft → Submitted
        draft.Submit();

        // Create the finalized IntakeRecord linked to the appointment (AC-4)
        var record = new IntakeRecord
        {
            PatientId = patientId,
            AppointmentId = request.AppointmentId,
            FormData = draft.FormData,
            AiPopulatedFields = draft.AiPopulatedFields,
            SubmittedAt = DateTimeOffset.UtcNow,
        };

        _context.IntakeRecords.Add(record);

        // Back-link: update the appointment's IntakeRecordId so both sides of the
        // relationship are populated (Appointment was created first without the intake record).
        var appointment = await _context.Appointments
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct);
        if (appointment is not null)
            appointment.IntakeRecordId = record.Id;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Intake submitted: patient={PatientId} appointment={AppointmentId} record={RecordId}",
            patientId,
            request.AppointmentId,
            record.Id);

        return new SubmitIntakeResponse
        {
            IntakeRecordId = record.Id,
            AppointmentId = record.AppointmentId,
            SubmittedAt = record.SubmittedAt,
        };
    }
}
