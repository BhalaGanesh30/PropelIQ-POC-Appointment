using PropelIQ.Modules.ClinicalIntelligence.Application.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.Services;

/// <summary>
/// Deterministic normalization rules for clinical entity names.
/// Supplements AI model output with rule-based standardization (AIR-001 hybrid pattern):
/// (a) medication brand → generic canonical form,
/// (b) allergy shorthand → full terminology,
/// (c) diagnosis text → ICD-10 prefix hint where a high-confidence mapping exists.
///
/// These tables are intentionally concise.  A production deployment would replace or
/// supplement them with RxNorm/SNOMED code-system lookups.
/// </summary>
public sealed class NormalizationService : INormalizationService
{
    // ── Medication: brand name (lower) → canonical name ──────────────────────
    private static readonly Dictionary<string, string> MedicationMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["tylenol"]       = "acetaminophen/tylenol",
            ["advil"]         = "ibuprofen/advil",
            ["motrin"]        = "ibuprofen/motrin",
            ["aleve"]         = "naproxen/aleve",
            ["aspirin"]       = "aspirin (ASA)",
            ["zocor"]         = "simvastatin/zocor",
            ["lipitor"]       = "atorvastatin/lipitor",
            ["crestor"]       = "rosuvastatin/crestor",
            ["zithromax"]     = "azithromycin/zithromax",
            ["augmentin"]     = "amoxicillin-clavulanate/augmentin",
            ["glucophage"]    = "metformin/glucophage",
            ["lasix"]         = "furosemide/lasix",
            ["norvasc"]       = "amlodipine/norvasc",
            ["lisinopril"]    = "lisinopril",
            ["metformin"]     = "metformin",
        };

    // ── Allergy: shorthand → standardized term ────────────────────────────────
    private static readonly Dictionary<string, string> AllergyMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["pcn"]          = "penicillin",
            ["pcn allergy"]  = "penicillin allergy",
            ["nsaid"]        = "NSAID",
            ["sulfa"]        = "sulfonamide antibiotics",
            ["sulfa allergy"]= "sulfonamide antibiotic allergy",
            ["asa"]          = "aspirin (ASA)",
            ["ace inhibitor"]= "ACE inhibitor",
        };

    // ── Diagnosis: common terms → ICD-10 prefix annotation ───────────────────
    private static readonly Dictionary<string, string> DiagnosisIcd10Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["hypertension"]                     = "hypertension (I10)",
            ["type 2 diabetes"]                  = "type 2 diabetes mellitus (E11)",
            ["type 2 diabetes mellitus"]         = "type 2 diabetes mellitus (E11)",
            ["type 1 diabetes"]                  = "type 1 diabetes mellitus (E10)",
            ["copd"]                             = "COPD (J44)",
            ["asthma"]                           = "asthma (J45)",
            ["atrial fibrillation"]              = "atrial fibrillation (I48)",
            ["heart failure"]                    = "heart failure (I50)",
            ["congestive heart failure"]         = "congestive heart failure (I50)",
            ["chronic kidney disease"]           = "chronic kidney disease (N18)",
            ["ckd"]                              = "chronic kidney disease (N18)",
            ["hyperlipidemia"]                   = "hyperlipidemia (E78)",
            ["depression"]                       = "major depressive disorder (F33)",
            ["anxiety"]                          = "anxiety disorder (F41)",
        };

    /// <inheritdoc />
    public IReadOnlyList<ExtractedFact> Normalize(IReadOnlyList<ExtractedFact> facts)
    {
        var result = new List<ExtractedFact>(facts.Count);

        foreach (var fact in facts)
        {
            result.Add(fact.FactType.ToLowerInvariant() switch
            {
                "medication" => NormalizeMedication(fact),
                "allergy"    => NormalizeAllergy(fact),
                "diagnosis"  => NormalizeDiagnosis(fact),
                _            => NormalizeBase(fact),   // finding — trim whitespace only
            });
        }

        return result;
    }

    // ── Per-type helpers ──────────────────────────────────────────────────────

    private static ExtractedFact NormalizeMedication(ExtractedFact fact)
    {
        var nameKey = fact.Name.Trim().ToLowerInvariant();
        var name    = MedicationMap.TryGetValue(nameKey, out var canonical) ? canonical : fact.Name.Trim();
        return fact with { Name = name, Value = fact.Value.Trim() };
    }

    private static ExtractedFact NormalizeAllergy(ExtractedFact fact)
    {
        var nameKey = fact.Name.Trim().ToLowerInvariant();
        var name    = AllergyMap.TryGetValue(nameKey, out var standard) ? standard : fact.Name.Trim();
        return fact with { Name = name, Value = fact.Value.Trim() };
    }

    private static ExtractedFact NormalizeDiagnosis(ExtractedFact fact)
    {
        var nameKey  = fact.Name.Trim().ToLowerInvariant();
        var hasCode  = DiagnosisIcd10Map.TryGetValue(nameKey, out var annotated);
        var name     = fact.Name.Trim();
        var value    = hasCode ? annotated! : fact.Value.Trim();
        return fact with { Name = name, Value = value };
    }

    private static ExtractedFact NormalizeBase(ExtractedFact fact) =>
        fact with { Name = fact.Name.Trim(), Value = fact.Value.Trim() };
}
