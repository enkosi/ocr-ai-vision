using OcrAiVision.Domain.Abstractions;

namespace OcrAiVision.Domain.Documents.ValueObjects;

/// <summary>
/// The MIME type of an uploaded document, normalised to lower case with any
/// parameters (such as <c>; charset=utf-8</c>) stripped.
/// </summary>
public sealed class DocumentMediaType : ValueObject
{
    private DocumentMediaType(string value) => Value = value;

    /// <summary>The normalised media type, for example <c>application/pdf</c>.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates a media type from a raw <c>Content-Type</c> header value.
    /// </summary>
    public static DocumentMediaType Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Media type must not be empty.");
        }

        var withoutParameters = value.Split(';')[0].Trim().ToLowerInvariant();

        if (withoutParameters.Length == 0)
        {
            throw new DomainException("Media type must not be empty.");
        }

        return new DocumentMediaType(withoutParameters);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
