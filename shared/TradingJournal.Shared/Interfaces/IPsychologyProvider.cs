namespace TradingJournal.Shared.Interfaces;

public interface IPsychologyProvider
{
    Task<List<string>> GetPsychologyByDate(DateTime date, CancellationToken cancellationToken);
    Task<List<string>> GetPsychologyByPeriod(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken);
}
