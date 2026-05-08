using PropelIQ.Modules.Insurance.Application.Dto;

namespace PropelIQ.Modules.Insurance.Application.Abstractions;

/// <summary>
/// Insurance verification report service (EP-005 US_039).
///
/// Provides paginated listing, PDF export, and CSV export of insurance profiles
/// with decrypted sensitive fields.  All methods are safe to call concurrently —
/// each creates a fresh EF Core query scope.
///
/// AC-1: Listing returns all verification records with their status.
/// AC-2: Status filter applied server-side; Redis caching (30s TTL) meets 500ms p95 (NFR-002).
/// AC-3: PDF export returns A4-formatted byte array via QuestPDF.
/// AC-4: CSV export returns billing-compatible CSV byte array via CsvHelper.
/// Edge Case 1: Export methods return ALL filtered records without pagination.
/// Edge Case 2: Staff/Admin role enforcement is applied at the controller level.
/// </summary>
public interface IInsuranceReportService
{
    /// <summary>
    /// Returns a paginated, optionally filtered and sorted page of insurance
    /// verification records.  Results are Redis-cached (30s TTL) keyed by
    /// filter parameters to meet NFR-002 500ms p95 (AC-2).
    /// </summary>
    Task<VerificationReportPagedResultDto> GetPagedReportAsync(
        VerificationReportFilterDto filter,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a PDF document containing ALL records that match the optional
    /// <paramref name="statusFilter"/> filter (Edge Case 1 — no pagination).
    /// Returns the PDF as a <c>byte[]</c>.
    /// </summary>
    Task<byte[]> GeneratePdfAsync(
        ValidationStatus? statusFilter,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a CSV file containing ALL records that match the optional
    /// <paramref name="statusFilter"/> filter (Edge Case 1 — no pagination).
    /// CSV headers are billing-system-compatible: PatientName, ProviderName,
    /// PolicyNumber, ValidationStatus, ValidatedAt.
    /// Returns the CSV as a <c>byte[]</c>.
    /// </summary>
    Task<byte[]> GenerateCsvAsync(
        ValidationStatus? statusFilter,
        CancellationToken ct = default);
}
