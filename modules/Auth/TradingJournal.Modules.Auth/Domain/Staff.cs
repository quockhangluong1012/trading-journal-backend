using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Auth;

[Table(name: "Staffs", Schema = "Auth")]
public sealed class Staff : EntityBase<int>
{
    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public required string FullName { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiry { get; set; }

    public bool IsActive { get; set; } = true;
}
