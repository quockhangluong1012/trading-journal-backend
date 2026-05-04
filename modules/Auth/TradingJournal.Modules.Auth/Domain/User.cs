using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Auth;

[Table(name: "Users", Schema = "Auth")]
public sealed class User : EntityBase<int>
{
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public required string FullName { get; set; }

    public string? RefreshToken { get; set; }

    public DateTimeOffset? RefreshTokenExpiry { get; set; }

    public bool IsActive { get; set; } = true;
}
