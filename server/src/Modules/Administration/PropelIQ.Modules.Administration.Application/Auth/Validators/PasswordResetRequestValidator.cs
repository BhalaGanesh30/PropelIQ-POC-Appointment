using FluentValidation;

namespace PropelIQ.Modules.Administration.Application.Auth.Validators;

/// <summary>
/// Validates the forgot-password request — only a valid email is required (us_018 AC-1).
/// </summary>
public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);
    }
}

/// <summary>
/// Validates the reset-password request — enforces AC-2 password complexity:
/// 8+ characters, 1 uppercase, 1 digit, 1 special character.
/// </summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Token)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
                .WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]")
                .WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]")
                .WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"\d")
                .WithMessage("Password must contain at least one digit.")
            .Matches(@"[^a-zA-Z0-9]")
                .WithMessage("Password must contain at least one special character.")
            .MaximumLength(256);
    }
}
