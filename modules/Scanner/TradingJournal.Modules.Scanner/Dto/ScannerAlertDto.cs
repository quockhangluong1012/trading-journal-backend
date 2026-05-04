namespace TradingJournal.Modules.Scanner.Dto;

public record ScannerAlertDto(
    int Id,
    string Symbol,
    string PatternType,
    string Timeframe,
    string DetectionTimeframe,
    decimal PriceAtDetection,
    decimal? ZoneHighPrice,
    decimal? ZoneLowPrice,
    string Description,
    int ConfluenceScore,
    string Regime,
    DateTimeOffset DetectedAt,
    bool IsDismissed);
