namespace TradingJournal.Shared.Security;

public interface IUserContext
{
    int UserId { get; }
    bool IsAuthenticated { get; }
}
