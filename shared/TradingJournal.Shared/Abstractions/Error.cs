namespace TradingJournal.Shared.Abstractions;

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error SqlException => new("Error.SqlException", "Database error occurred.");

    public static Error ValidationError(string errorCode, string errorMessage) => new(errorCode, errorMessage);

    public static Error UnexpectedError => new("Error.UnexpectedError", "An unexpected error occurred.");

    public static Error InvalidInput => new("Error.InvalidInput", "Invalid input.");

    public static Error NotFound => new("Error.NotFound", "Resource not found.");

    public static Error Unauthorized => new("Error.Unauthorized", "Unauthorized.");

    public static Error Create(string errorMessage) => new("Error.Create", errorMessage);

    public static Error Create(string errorCode, string errorMessage) => new(errorCode, errorMessage);
}
