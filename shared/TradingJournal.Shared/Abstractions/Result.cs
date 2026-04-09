namespace TradingJournal.Shared.Abstractions;

public class Result
{
    protected Result(bool isSuccess, List<Error> errors)
    {
        if (isSuccess && errors.Count > 0)
            throw new InvalidOperationException("A successful result cannot have errors.");

        if (!isSuccess && errors.Count == 0)
            throw new InvalidOperationException("A failed result must have at least one error.");

        IsSuccess = isSuccess;
        Errors = errors;
    }

    // Private parameterless constructor for success (no errors)
    private Result()
    {
        IsSuccess = true;
        Errors = [];
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public List<Error> Errors { get; }

    public static Result Success() => new();

    public static Result Failure(Error error) => new(false, [error]);

    public static Result Failure(List<Error> errors) => new(false, errors);
}

public class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true, [])
    {
        _value = value;
    }

    private Result(List<Error> errors) : base(false, errors)
    {
        _value = default;
    }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("The value of a failure result can not be accessed.");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new([error]);

    public static new Result<T> Failure(List<Error> errors) => new(errors);

    public static implicit operator Result<T>(T value) => Success(value);
}

