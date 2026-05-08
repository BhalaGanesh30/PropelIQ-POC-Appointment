namespace PropelIQ.Modules.ClinicalIntelligence.Domain.Enums;

/// <summary>
/// Constrained set of document categories (AC-1, FR-DM-004).
/// Mapped to the <c>document_category_type</c> PostgreSQL enum via Npgsql.
/// Values use snake_case to match the PostgreSQL enum labels.
/// </summary>
public enum DocumentCategoryType
{
    LabReport,
    Referral,
    Prescription,
    Imaging,
    Insurance,
    Other,
}
