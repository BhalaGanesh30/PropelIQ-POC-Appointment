namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Entities;

/// <summary>
/// Join table entity representing a clinician's personal code favorites (US_052, task_003).
///
/// Maps to the <c>app.user_code_favorites</c> table. The composite PK is
/// (<c>UserId</c>, <c>CodeType</c>, <c>Code</c>).
///
/// Used by:
/// - <c>CodeFavoriteRepository</c> — CRUD operations for favorites management.
/// - <c>CodeSearchService</c>      — join for pinning favorited codes at the top of results.
/// </summary>
public sealed class UserCodeFavorite
{
    /// <summary>FK to the user who created this favorite.</summary>
    public required Guid UserId { get; set; }

    /// <summary>Code type discriminator: "icd10" or "cpt".</summary>
    public required string CodeType { get; set; }

    /// <summary>The favorited code value, e.g. "E11.9" or "99213".</summary>
    public required string Code { get; set; }

    /// <summary>UTC timestamp when the favorite was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
