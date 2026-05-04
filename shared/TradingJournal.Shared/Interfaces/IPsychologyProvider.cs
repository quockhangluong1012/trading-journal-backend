namespace TradingJournal.Shared.Interfaces;

public interface IPsychologyProvider
{
    Task<List<string>> GetPsychologyByDate(DateTimeOffset date, CancellationToken cancellationToken);
    Task<List<string>> GetPsychologyByPeriod(DateTimeOffset fromDate, DateTimeOffset toDate, CancellationToken cancellationToken);
}
