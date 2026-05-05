namespace TradingJournal.Modules.Auth.Features.V1.Staffs.Models;

public record StaffDto(int Id, string Email, string FullName, bool IsActive, DateTime CreatedDate);
