namespace TradingJournal.Modules.Auth.Common;

/// <summary>
/// Shared password-strength rules applied wherever a password is *set* (registration,
/// staff create/update) — never at login, where only presence is checked. Centralising the
/// policy keeps users and staff on the same minimum length and complexity requirements.
/// </summary>
internal static class PasswordRules
{
    public const int MinimumLength = 8;

    /// <summary>
    /// Requires the password to meet the minimum length and contain at least one uppercase
    /// letter, one lowercase letter, one digit, and one special character. Combine with a
    /// preceding <c>.Cascade(CascadeMode.Stop)</c> (and <c>.NotEmpty()</c> where required) so a
    /// single, most-relevant message is returned.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> StrongPassword<T>(this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder
            .MinimumLength(MinimumLength)
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage($"Password must be at least {MinimumLength} characters.")
            .Matches("[A-Z]")
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]")
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]")
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]")
                .WithErrorCode(HttpStatusCode.BadRequest.ToString())
                .WithMessage("Password must contain at least one special character.");
    }
}
