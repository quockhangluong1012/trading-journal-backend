namespace TradingJournal.Modules.Scanner.Dto;

public record WatchlistDto(
    int Id,
    string Name,
    bool IsActive,
    bool IsScannerRunning,
    DateTimeOffset CreatedDate,
    List<WatchlistAssetDto> Assets);

public record WatchlistAssetDto(
    int Id,
    string Symbol,
    string DisplayName,
    List<string> EnabledDetectors);

public record UpdateAssetDetectorsRequest(
    List<string> EnabledPatterns);

public record CreateWatchlistRequest(
    string Name,
    List<CreateWatchlistAssetRequest> Assets);

public record CreateWatchlistAssetRequest(
    string Symbol,
    string DisplayName);

public record UpdateWatchlistRequest(
    string Name,
    bool IsActive,
    List<CreateWatchlistAssetRequest> Assets);
