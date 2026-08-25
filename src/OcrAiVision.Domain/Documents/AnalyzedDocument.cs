using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Domain.Documents;

/// <summary>
/// The aggregate root describing what a Document Intelligence model recovered
/// from one document: its text, its pages, its tables and its labelled fields.
/// </summary>
public sealed class AnalyzedDocument
{
    private readonly List<DocumentPage> _pages;
    private readonly List<DocumentTable> _tables;
    private readonly List<ExtractedField> _fields;

    private AnalyzedDocument(
        string sourceFileName,
        ModelId modelId,
        string content,
        List<DocumentPage> pages,
        List<DocumentTable> tables,
        List<ExtractedField> fields)
    {
        SourceFileName = sourceFileName;
        ModelId = modelId;
        Content = content;
        _pages = pages;
        _tables = tables;
        _fields = fields;
    }

    /// <summary>The name of the file that was analysed.</summary>
    public string SourceFileName { get; }

    /// <summary>The model that produced this result.</summary>
    public ModelId ModelId { get; }

    /// <summary>The full text of the document.</summary>
    public string Content { get; }

    /// <summary>The recognised pages, ordered by page number.</summary>
    public IReadOnlyList<DocumentPage> Pages => _pages;

    /// <summary>The recognised tables.</summary>
    public IReadOnlyList<DocumentTable> Tables => _tables;

    /// <summary>The labelled values the model extracted.</summary>
    public IReadOnlyList<ExtractedField> Fields => _fields;

    /// <summary>How many pages the model read.</summary>
    public int PageCount => _pages.Count;

    /// <summary>Whether the model recovered any text at all.</summary>
    public bool HasContent => Content.Length > 0;

    /// <summary>
    /// Creates an analysis result.
    /// </summary>
    public static AnalyzedDocument Create(
        string sourceFileName,
        ModelId modelId,
        string? content,
        IEnumerable<DocumentPage>? pages = null,
        IEnumerable<DocumentTable>? tables = null,
        IEnumerable<ExtractedField>? fields = null)
    {
        ArgumentNullException.ThrowIfNull(modelId);

        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new DomainException("An analysed document must record the file it came from.");
        }

        var orderedPages = (pages ?? [])
            .OrderBy(page => page.Number.Value)
            .ToList();

        var duplicatePage = orderedPages
            .GroupBy(page => page.Number.Value)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatePage is not null)
        {
            throw new DomainException($"Page {duplicatePage.Key} appears more than once.");
        }

        return new AnalyzedDocument(
            sourceFileName.Trim(),
            modelId,
            content ?? string.Empty,
            orderedPages,
            (tables ?? []).ToList(),
            (fields ?? []).ToList());
    }

    /// <summary>
    /// The lowest confidence across the extracted fields, which is the useful
    /// number when deciding whether a result needs a human to look at it.
    /// Returns <see langword="null"/> when the model extracted no fields.
    /// </summary>
    public ConfidenceScore? LowestFieldConfidence() =>
        _fields.Count == 0
            ? null
            : _fields.MinBy(field => field.Confidence.Value)!.Confidence;

    /// <summary>
    /// Whether every extracted field met the supplied confidence threshold.
    /// A document with no fields is treated as meeting the threshold.
    /// </summary>
    public bool MeetsConfidenceThreshold(ConfidenceScore threshold)
    {
        ArgumentNullException.ThrowIfNull(threshold);

        return _fields.TrueForAll(field => field.Confidence.Value >= threshold.Value);
    }
}
