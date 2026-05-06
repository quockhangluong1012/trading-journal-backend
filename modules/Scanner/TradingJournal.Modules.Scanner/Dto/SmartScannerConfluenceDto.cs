namespace TradingJournal.Modules.Scanner.Dto;

public sealed record SmartScannerConfluenceSignalDto(
    string Timeframe,
    decimal PriceAtDetection,
    decimal? ZoneHigh,
    decimal? ZoneLow,
    string Description,
    DateTime DetectedAt);

public sealed record SmartScannerConfluenceCandidateDto(
    string PatternType,
    int ConfluenceScore,
    IReadOnlyList<string> ConfirmingTimeframes,
    IReadOnlyList<SmartScannerConfluenceSignalDto> Signals);

public sealed record SmartScannerConfluenceDto(
    string Symbol,
    string EconomicRiskState,
    string EconomicRiskMessage,
    int MinConfluenceScore,
    int MaxConfluenceScore,
    IReadOnlyList<SmartScannerConfluenceCandidateDto> Candidates);