namespace OcrAiVision.Application.Abstractions;

/// <summary>
/// The outcome of a use case: either a value or an <see cref="Abstractions.Error"/>.
/// Expected failures travel as values rather than exceptions, which keeps the
/// happy path readable and stops transport concerns leaking into catch blocks.
/// </summary>
/// <typeparam name="TValue">The type produced on success.</typeparam>
public readonly struct Result<TValue>
{
    private readonly TValue? _value;
    private readonly Error? _error;

    private Result(TValue value)
    {
        _value = value;
        _error = null;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    /// <summary>Whether the use case produced a value.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the use case produced an error.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The produced value.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    /// <summary>The produced error.</summary>
    /// <exception cref="InvalidOperationException">The result is a success.</exception>
    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("A successful result has no error.");

    /// <summary>Creates a successful result.</summary>
    public static Result<TValue> Success(TValue value) => new(value);

    /// <summary>Creates a failed result.</summary>
    public static Result<TValue> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<TValue>(error);
    }

    /// <summary>Implicitly wraps a value in a successful result.</summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Implicitly wraps an error in a failed result.</summary>
    public static implicit operator Result<TValue>(Error error) => Failure(error);

    /// <summary>
    /// Collapses the result into a single value by applying whichever function matches the outcome.
    /// </summary>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }
}
