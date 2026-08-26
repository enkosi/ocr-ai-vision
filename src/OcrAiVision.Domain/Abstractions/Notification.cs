namespace OcrAiVision.Domain.Abstractions;

/// <summary>
/// Fowler's Notification pattern: collects every rule violation found while
/// checking a candidate object, so a caller can report all of them at once
/// instead of failing on the first.
/// </summary>
public sealed class Notification
{
    private readonly List<string> _errors = [];

    /// <summary>The violations recorded so far, in the order they were added.</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>Whether any violation has been recorded.</summary>
    public bool HasErrors => _errors.Count > 0;

    /// <summary>Whether the candidate satisfied every rule.</summary>
    public bool IsValid => !HasErrors;

    /// <summary>
    /// Records a violation. Blank messages are ignored so callers can pass
    /// conditional strings without guarding first.
    /// </summary>
    public Notification AddError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            _errors.Add(message);
        }

        return this;
    }

    /// <summary>
    /// Records a violation when <paramref name="condition"/> holds.
    /// </summary>
    public Notification AddErrorIf(bool condition, string message) =>
        condition ? AddError(message) : this;

    /// <summary>
    /// Joins the recorded violations into a single human-readable sentence.
    /// </summary>
    public override string ToString() => string.Join(" ", _errors);
}
