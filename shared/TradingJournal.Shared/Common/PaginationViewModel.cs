namespace TradingJournal.Shared.Common;

public sealed class PaginationViewModel<T> where T : class
{
    public int TotalItems { get; set; }

    public bool HasMore { get; set; }

    public IReadOnlyCollection<T> Values { get; set; } = [];
}
