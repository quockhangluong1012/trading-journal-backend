namespace TradingJournal.Modules.Psychology.Helpers;

internal sealed class PsychologyProvider(IPsychologyDbContext context) : IPsychologyProvider
{
    public async Task<List<string>> GetPsychologyByDate(DateTime date, CancellationToken cancellationToken)
    {
        PsychologyJournal? psychologyJournal = (await LoadPsychologyJournalsAsync(date, date, cancellationToken))
            .FirstOrDefault();

        if (psychologyJournal == null)
        {
            return [];
        }

        List<string> emotions = [.. psychologyJournal.PsychologyJournalEmotions
            .Select(e => e.EmotionTag.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))];

        List<string> summary = [
            psychologyJournal.TodayTradingReview,
            psychologyJournal.ConfidentLevel.ToString(),
            psychologyJournal.OverallMood.ToString(),
        ];

        summary.AddRange(emotions);

        return summary;
    }

    public async Task<List<string>> GetPsychologyByPeriod(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
    {
        List<PsychologyJournal> journals = await LoadPsychologyJournalsAsync(fromDate, toDate, cancellationToken);

        return [.. journals.Select(BuildPeriodSummary)];
    }

    private async Task<List<PsychologyJournal>> LoadPsychologyJournalsAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        DateTime start = fromDate.Date;
        DateTime end = toDate.Date.AddDays(1).AddTicks(-1);

        return await context.PsychologyJournals
            .AsNoTracking()
            .Include(pj => pj.PsychologyJournalEmotions)
                .ThenInclude(journalEmotion => journalEmotion.EmotionTag)
            .Where(pj => pj.Date >= start && pj.Date <= end)
            .OrderByDescending(pj => pj.Date)
            .ToListAsync(cancellationToken);
    }

    private static string BuildPeriodSummary(PsychologyJournal journal)
    {
        string emotions = string.Join(", ", journal.PsychologyJournalEmotions
            .Select(entry => entry.EmotionTag.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name)));

        return $"- {journal.Date:yyyy-MM-dd} | Mood: {journal.OverallMood} | Confidence: {journal.ConfidentLevel} | Emotions: {(string.IsNullOrWhiteSpace(emotions) ? "None" : emotions)} | Note: {(string.IsNullOrWhiteSpace(journal.TodayTradingReview) ? "No journal note" : journal.TodayTradingReview)}";
    }
}
