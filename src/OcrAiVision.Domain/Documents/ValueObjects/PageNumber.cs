using System.Globalization;
using OcrAiVision.Domain.Abstractions;

namespace OcrAiVision.Domain.Documents.ValueObjects;

/// <summary>
/// A one-based page ordinal within a document.
/// </summary>
public sealed class PageNumber : ValueObject
{
    private PageNumber(int value) => Value = value;

    /// <summary>The one-based page ordinal.</summary>
    public int Value { get; }

    /// <summary>
    /// Creates a page number, rejecting zero and negative ordinals.
    /// </summary>
    public static PageNumber Create(int value)
    {
        if (value < 1)
        {
            throw new DomainException($"Page numbers start at 1 but was {value}.");
        }

        return new PageNumber(value);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
