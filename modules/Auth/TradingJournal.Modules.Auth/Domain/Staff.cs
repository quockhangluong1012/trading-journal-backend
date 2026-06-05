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

    /// <summary>
    /// Whether this staff member has administrative privileges. Only staff with this flag
    /// receive the "Admin" role claim; everyone else gets the lower-privileged "Staff" role.
    /// </summary>
    public bool IsAdmin { get; set; }
}
