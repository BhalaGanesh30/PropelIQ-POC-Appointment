using System.ComponentModel.DataAnnotations;

namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Query-parameter DTO for the insurance verification report listing endpoint
/// (EP-005 US_039 AC-2, Edge Case 1).
///
/// All parameters are optional with sensible defaults so callers can add filters
/// incrementally without breaking changes.
/// </summary>
public sealed record VerificationReportFilterDto
{
    /// <summary>
    /// Optional status filter.  When null all statuses are returned (AC-1).
    /// Maps to <c>insurance_profiles.verification_status</c>.
    /// </summary>
    public ValidationStatus? Status { get; init; }

    /// <summary>1-indexed page number (default 1).</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>Records per page (default 25, max 100 per Edge Case 1).</summary>
    [Range(1, 100)]
    public int PageSize { get; init; } = 25;

    /// <summary>
    /// Column to sort by.  Accepted values: <c>patientName</c>, <c>providerName</c>,
    /// <c>policyNumber</c>, <c>validationStatus</c>, <c>validatedAt</c>.
    /// Defaults to <c>validatedAt</c> when unset or invalid.
    /// </summary>
    public string? SortBy { get; init; }

    /// <summary>Sort direction: <c>asc</c> or <c>desc</c> (default <c>desc</c>).</summary>
    public string? SortDirection { get; init; }
}
