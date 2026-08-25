using OcrAiVision.Domain.Abstractions;

namespace OcrAiVision.Domain.Documents.ValueObjects;

/// <summary>
/// Identifies the Document Intelligence model used to analyse a document,
/// for example <c>prebuilt-layout</c> or <c>prebuilt-invoice</c>.
/// </summary>
public sealed class ModelId : ValueObject
{
    /// <summary>The general-purpose layout model used when a caller does not choose one.</summary>
    public static readonly ModelId PrebuiltLayout = new("prebuilt-layout");

    /// <summary>The general document/read model.</summary>
    public static readonly ModelId PrebuiltRead = new("prebuilt-read");

    private ModelId(string value) => Value = value;

    /// <summary>The model identifier as understood by the analysis service.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a model identifier, trimming surrounding whitespace.
    /// </summary>
    public static ModelId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Model id must not be empty.");
        }

        return new ModelId(value.Trim());
    }

    /// <summary>
    /// Creates a model identifier, falling back to <see cref="PrebuiltLayout"/> when none is supplied.
    /// </summary>
    public static ModelId CreateOrDefault(string? value) =>
        string.IsNullOrWhiteSpace(value) ? PrebuiltLayout : Create(value);

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
