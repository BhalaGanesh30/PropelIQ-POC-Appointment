namespace PropelIQ.Modules.ClinicalIntelligence.Application.Dto;

/// <summary>
/// A single audit entry in a clinical fact's edit/verify history (US_047 AC-3).
/// Returned by <c>GET /api/v1/clinical-facts/{id}/history</c>.
/// </summary>
public sealed record FactAuditEntryDto
{
    /// <summary>Primary key of the underlying <c>audit_records</c> row.</summary>
    public Guid AuditId { get; init; }

    /// <summary>Machine-readable event type: <c>fact_edited</c> or <c>fact_verified</c>.</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>Fact name prior to this edit. Null for verify-only events.</summary>
    public string? PreviousName { get; init; }

    /// <summary>Fact value prior to this edit. Null for verify-only events.</summary>
    public string? PreviousValue { get; init; }

    /// <summary>Display name of the clinician who performed the action.</summary>
    public string EditorDisplayName { get; init; } = string.Empty;

    /// <summary>UTC timestamp when the event occurred.</summary>
    public DateTimeOffset Timestamp { get; init; }
}
