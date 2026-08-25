using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Application.UnitTests.Documents;

/// <summary>
/// A test data builder for the domain aggregate the analyzer port returns.
/// </summary>
internal sealed class AnalyzedDocumentBuilder
{
    private readonly List<DocumentPage> _pages = [];
    private readonly List<DocumentTable> _tables = [];
    private readonly List<ExtractedField> _fields = [];
    private string _fileName = "invoice.pdf";
    private ModelId _modelId = ModelId.PrebuiltLayout;
    private string _content = "INVOICE";

    public static AnalyzedDocumentBuilder AnAnalysis() => new();

    public AnalyzedDocumentBuilder Of(string fileName)
    {
        _fileName = fileName;
        return this;
    }

    public AnalyzedDocumentBuilder ByModel(string modelId)
    {
        _modelId = ModelId.Create(modelId);
        return this;
    }

    public AnalyzedDocumentBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public AnalyzedDocumentBuilder WithPage(int number, string content, int lines = 1, int words = 1)
    {
        _pages.Add(DocumentPage.Create(PageNumber.Create(number), content, lines, words));
        return this;
    }

    public AnalyzedDocumentBuilder WithField(string name, string? value, double confidence, int? page = null)
    {
        _fields.Add(ExtractedField.Create(
            name,
            value,
            ConfidenceScore.Create(confidence),
            page is null ? null : PageNumber.Create(page.Value)));

        return this;
    }

    public AnalyzedDocumentBuilder WithTable(int rows, int columns, params DocumentTableCell[] cells)
    {
        _tables.Add(DocumentTable.Create(rows, columns, cells, PageNumber.Create(1)));
        return this;
    }

    public AnalyzedDocument Build() =>
        AnalyzedDocument.Create(_fileName, _modelId, _content, _pages, _tables, _fields);
}
