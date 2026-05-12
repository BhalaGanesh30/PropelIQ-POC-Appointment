using FluentValidation;

namespace PropelIQ.Modules.SharedServices.Application.Compliance.Validators;

/// <summary>
/// FluentValidation validator for <see cref="ReportRequest"/> (US_058, AC-4).
///
/// Registered via <c>AddValidatorsFromAssemblyContaining&lt;ReportRequestValidator&gt;()</c>
/// in <c>Program.cs</c>; auto-validates <c>[FromBody]</c> payloads before the
/// controller action runs.
/// </summary>
public sealed class ReportRequestValidator : AbstractValidator<ReportRequest>
{
    private static readonly TimeSpan MaxSpan = TimeSpan.FromDays(365 * 2); // 2-year limit

    public ReportRequestValidator()
    {
        RuleFor(r => r.ReportType)
            .NotEmpty()
            .WithMessage("ReportType is required.")
            .Must(rt => ReportTypes.All.Contains(rt))
            .WithMessage($"ReportType must be one of: {string.Join(", ", ReportTypes.All)}.");

        RuleFor(r => r.PeriodStartUtc)
            .NotEmpty()
            .WithMessage("PeriodStartUtc is required.");

        RuleFor(r => r.PeriodEndUtc)
            .NotEmpty()
            .WithMessage("PeriodEndUtc is required.")
            .GreaterThan(r => r.PeriodStartUtc)
            .WithMessage("PeriodEndUtc must be after PeriodStartUtc.");

        // Prevent excessively wide queries that would exhaust memory (DR-005).
        RuleFor(r => r)
            .Must(r => (r.PeriodEndUtc - r.PeriodStartUtc) <= MaxSpan)
            .WithMessage("Date range must not exceed 2 years.")
            .OverridePropertyName("PeriodEndUtc");
    }
}
