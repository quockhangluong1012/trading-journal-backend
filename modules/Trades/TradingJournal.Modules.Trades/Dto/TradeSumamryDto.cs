namespace TradingJournal.Modules.Trades.Dto;

public sealed record TradeSumamryDto(string Asset,
        string Position,
        double EntryPrice,
        double TargetTier1,
        double? TargetTier2,
        double? TargetTier3,
        double StopLoss,
        string Notes,
        double? ExitPrice,
        double? Pnl,
        List<string>? TradeTechnicalAnalysisTags,
        List<string>? EmotionTags,
        string ConfidenceLevel,
        List<string> TradeHistoryChecklists,
        string TradingZone,
        DateTime OpenDate,
        DateTime ClosedDate);

