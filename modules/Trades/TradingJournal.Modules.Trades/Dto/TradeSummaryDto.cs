namespace TradingJournal.Modules.Trades.Dto;

public sealed record TradeSummaryDto(string Asset,
        string Position,
        decimal EntryPrice,
        decimal TargetTier1,
        decimal? TargetTier2,
        decimal? TargetTier3,
        decimal StopLoss,
        string Notes,
        decimal? ExitPrice,
        decimal? Pnl,
        List<string>? TradeTechnicalAnalysisTags,
        List<string>? EmotionTags,
        string ConfidenceLevel,
        List<string> TradeHistoryChecklists,
        string TradingZone,
        DateTime OpenDate,
        DateTime ClosedDate);
