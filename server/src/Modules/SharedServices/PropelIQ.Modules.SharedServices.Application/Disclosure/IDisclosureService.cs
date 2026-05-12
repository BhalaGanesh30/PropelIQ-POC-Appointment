using PropelIQ.Modules.SharedServices.Domain.Entities;

namespace PropelIQ.Modules.SharedServices.Application.Disclosure;

// ── Request / response DTOs ──────────────────────────────────────────────────

/// <summary>Payload for a patient disclosure request submission (AC-2).</summary>
public sealed record SubmitDisclosureRequest(
    DateTimeOffset FromDateUtc,
    DateTimeOffset ToDateUtc);

/// <summary>Payload for a staff member approving or rejecting a disclosure (AC-3).</summary>
public sealed record ReviewDisclosureRequest(
    bool Approved,
    string? Notes);

/// <summary>Read model returned to callers of the disclosure service.</summary>
public sealed record DisclosureRequestDto(
    Guid Id,
    Guid PatientId,
    DateTimeOffset FromDateUtc,
    DateTimeOffset ToDateUtc,
    DisclosureStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompiledAt,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewNotes,
    DateTimeOffset? DeliveredAt,
    string? DeliveryMethod,
    Guid? ReportId);

/// <summary>Summary read model for the compiled report (staff preview).</summary>
public sealed record DisclosureReportDto(
    Guid Id,
    Guid DisclosureRequestId,
    int AccessEventCount,
    DateTimeOffset GeneratedAt,
    string ReportJson,
    bool HasDownloadLink);

/// <summary>Result of a patient report download attempt (edge case 1, AC-3).</summary>
public sealed record ReportDownloadResult(bool IsExpired, byte[] Content);

// ── Service contract ─────────────────────────────────────────────────────────

/// <summary>
/// Orchestrates patient disclosure request lifecycle (US_057, AC-2, AC-3, AC-4).
///
/// Implemented by <c>DisclosureService</c> in the Infrastructure layer.
/// </summary>
public interface IDisclosureService
{
    /// <summary>
    /// Creates a new disclosure request for the given patient and date range.
    /// Returns the new request ID (AC-2).
    /// </summary>
    Task<Guid> SubmitAsync(
        Guid patientId,
        DateTimeOffset fromDateUtc,
        DateTimeOffset toDateUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the disclosure request matching <paramref name="requestId"/> for the
    /// given patient, or <c>null</c> if not found / does not belong to the patient.
    /// </summary>
    Task<DisclosureRequestDto?> GetByIdForPatientAsync(
        Guid patientId,
        Guid requestId,
        CancellationToken ct = default);

    /// <summary>Lists all disclosure requests for the given patient (most-recent first).</summary>
    Task<IReadOnlyList<DisclosureRequestDto>> ListForPatientAsync(
        Guid patientId,
        CancellationToken ct = default);

    /// <summary>
    /// Validates the HMAC download token and returns the report bytes, or <c>null</c>
    /// when the request is not found / does not belong to the patient.
    /// <see cref="ReportDownloadResult.IsExpired"/> is <c>true</c> when the token
    /// has passed its 48-hour expiry window (edge case 1).
    /// </summary>
    Task<ReportDownloadResult?> GetReportForDownloadAsync(
        Guid patientId,
        Guid requestId,
        string token,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a paginated list of disclosure requests for staff review, optionally
    /// filtered by <paramref name="status"/>.
    /// </summary>
    Task<IReadOnlyList<DisclosureRequestDto>> ListForReviewAsync(
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Approves or rejects a disclosure request (AC-3).
    /// On approval: generates a 48-hour HMAC download token, emails the patient,
    /// and transitions the request to Delivered.
    /// Returns <c>false</c> when the request is not found.
    /// </summary>
    Task<bool> ReviewAsync(
        Guid requestId,
        Guid reviewerId,
        bool approved,
        string? notes,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the compiled report for staff preview prior to approval.
    /// Returns <c>null</c> when not found.
    /// </summary>
    Task<DisclosureReportDto?> GetReportForReviewAsync(
        Guid requestId,
        CancellationToken ct = default);
}
