using System.Globalization;
using OcrAiVision.Domain.Abstractions;

namespace OcrAiVision.Domain.Documents.ValueObjects;

/// <summary>
/// A normalised model confidence in the closed interval [0, 1].
/// </summary>
public sealed class ConfidenceScore : ValueObject
{
    /// <summary>The lowest confidence the model can report.</summary>
    public static readonly ConfidenceScore Minimum = new(0d);

    private ConfidenceScore(double value) => Value = value;

    /// <summary>The confidence, between 0 and 1 inclusive.</summary>
    public double Value { get; }

    /// <summary>The confidence expressed as a whole percentage, rounded to the nearest integer.</summary>
    public int Percentage => (int)Math.Round(Value * 100d, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Creates a score, rejecting values outside [0, 1] and non-finite values.
    /// </summary>
    public static ConfidenceScore Create(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new DomainException("Confidence must be a finite number.");
        }

        if (value is < 0d or > 1d)
        {
            throw new DomainException(
                $"Confidence must be between 0 and 1 but was {value.ToString(CultureInfo.InvariantCulture)}.");
        }

        return new ConfidenceScore(value);
    }

    /// <summary>
    /// Creates a score from a possibly absent model value, falling back to <see cref="Minimum"/>.
    /// </summary>
    public static ConfidenceScore CreateOrMinimum(double? value) =>
        value is null ? Minimum : Create(value.Value);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString("0.####", CultureInfo.InvariantCulture);
}
