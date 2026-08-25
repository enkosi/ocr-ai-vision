using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Domain.Documents;

/// <summary>
/// One page of a recognised document together with the text found on it.
/// </summary>
public sealed class DocumentPage
{
    private DocumentPage(PageNumber number, string content, int lineCount, int wordCount)
    {
        Number = number;
        Content = content;
        LineCount = lineCount;
        WordCount = wordCount;
    }

    /// <summary>The one-based page ordinal.</summary>
    public PageNumber Number { get; }

    /// <summary>The text recovered from this page.</summary>
    public string Content { get; }

    /// <summary>How many lines the model detected on this page.</summary>
    public int LineCount { get; }

    /// <summary>How many words the model detected on this page.</summary>
    public int WordCount { get; }

    /// <summary>
    /// Creates a page.
    /// </summary>
    public static DocumentPage Create(PageNumber number, string? content, int lineCount, int wordCount)
    {
        ArgumentNullException.ThrowIfNull(number);

        if (lineCount < 0)
        {
            throw new DomainException($"Line count cannot be negative but was {lineCount}.");
        }

        if (wordCount < 0)
        {
            throw new DomainException($"Word count cannot be negative but was {wordCount}.");
        }

        return new DocumentPage(number, content ?? string.Empty, lineCount, wordCount);
    }
}
