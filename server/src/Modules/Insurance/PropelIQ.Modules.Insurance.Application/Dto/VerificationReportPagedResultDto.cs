namespace PropelIQ.Modules.Insurance.Application.Dto;

/// <summary>
/// Paged wrapper returned by <c>GET /api/v1/insurance/verification-report</c>
/// (EP-005 US_039 AC-1, Edge Case 1).
///
/// The frontend uses <see cref="TotalCount"/> to compute the total page count for
/// the custom pagination bar (UXR-303).  Export endpoints return all filtered records
/// regardless of these pagination values.
/// </summary>
public sealed record VerificationReportPagedResultDto
{
    /// <summary>Records for the current page.</summary>
    public IReadOnlyList<VerificationReportEntryDto> Entries { get; init; }
        = Array.Empty<VerificationReportEntryDto>();

    /// <summary>Total number of records matching the current filter (pre-pagination).</summary>
    public int TotalCount { get; init; }

    /// <summary>1-indexed current page number.</summary>
    public int Page { get; init; }

    /// <summary>Records per page requested.</summary>
    public int PageSize { get; init; }
}
