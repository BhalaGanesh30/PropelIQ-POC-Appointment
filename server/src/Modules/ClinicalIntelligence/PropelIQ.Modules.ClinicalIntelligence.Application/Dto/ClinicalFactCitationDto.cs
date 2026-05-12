namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// A single clinical fact citation returned as supporting evidence for an ICD-10
/// suggestion (AC-2, AIR-004).
/// </summary>
public sealed record ClinicalFactCitationDto
{
    public required Guid FactId { get; init; }
    public required string FactType { get; init; }
    public required string Name { get; init; }
    public required string Value { get; init; }
    public DateTimeOffset? FactDate { get; init; }
}
