using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Domain.Documents;

/// <summary>
/// A single labelled value the model recovered from a document, such as an
/// invoice total or a key/value pair from a form.
/// </summary>
public sealed class ExtractedField : ValueObject
{
    private ExtractedField(string name, string? value, ConfidenceScore confidence, PageNumber? page)
    {
        Name = name;
        Value = value;
        Confidence = confidence;
        Page = page;
    }

    /// <summary>The field label, for example <c>InvoiceTotal</c>.</summary>
    public string Name { get; }

    /// <summary>The recovered value, or <see langword="null"/> when the model found the label but no content.</summary>
    public string? Value { get; }

    /// <summary>How confident the model is in this field.</summary>
    public ConfidenceScore Confidence { get; }

    /// <summary>The page the field was found on, when the model reported one.</summary>
    public PageNumber? Page { get; }

    /// <summary>
    /// Creates a field.
    /// </summary>
    public static ExtractedField Create(
        string name,
        string? value,
        ConfidenceScore confidence,
        PageNumber? page = null)
    {
        ArgumentNullException.ThrowIfNull(confidence);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("An extracted field must have a name.");
        }

        return new ExtractedField(name.Trim(), value, confidence, page);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Name;
        yield return Value;
        yield return Confidence;
        yield return Page;
    }
}
