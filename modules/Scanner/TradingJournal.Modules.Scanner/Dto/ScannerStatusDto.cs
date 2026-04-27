namespace TradingJournal.Modules.Scanner.Dto;

public record ScannerStatusDto(
    string Status,
    int ScanIntervalSeconds,
    List<string> EnabledPatterns,
    List<string> EnabledTimeframes,
    int MinConfluenceScore,
    DateTime? LastScanTime,
    int ActiveWatchlistCount,
    int TotalAssetsMonitored);
