namespace TradingJournal.Modules.Psychology.Helpers;

internal sealed class PsychologyProvider(IPsychologyDbContext context) : IPsychologyProvider
{
    public async Task<List<string>> GetPsychologyByDate(DateTime date, CancellationToken cancellationToken)
    {
        PsychologyJournal? psychologyJournal = await context.PsychologyJournals
            .AsNoTracking()
            .Where(pj => pj.Date.Date == date.Date)
            .FirstOrDefaultAsync(cancellationToken);

        if (psychologyJournal == null) {
            return [];
        }

        List<string> emotions = [.. psychologyJournal.PsychologyJournalEmotions.Select(e => e.EmotionTag.Name)];

        List<string> summary = [
            psychologyJournal.TodayTradingReview,
            psychologyJournal.ConfidentLevel.ToString(),
            psychologyJournal.OverallMood.ToString(),
        ];

        summary.AddRange(emotions);

        return summary;
    }
}
