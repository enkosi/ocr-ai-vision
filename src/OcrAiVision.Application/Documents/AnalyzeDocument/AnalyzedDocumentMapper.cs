using OcrAiVision.Domain.Documents;

namespace OcrAiVision.Application.Documents.AnalyzeDocument;

/// <summary>
/// Translates the domain aggregate into the use case's response DTO. Isolating
/// the translation keeps the interactor about policy and keeps the wire shape
/// in one place when it needs to change.
/// </summary>
internal static class AnalyzedDocumentMapper
{
    public static AnalyzeDocumentResponse ToResponse(this AnalyzedDocument document) =>
        new(
            document.SourceFileName,
            document.ModelId.Value,
            document.Content,
            document.PageCount,
            document.LowestFieldConfidence()?.Value,
            document.Pages.Select(ToDto).ToList(),
            document.Tables.Select(ToDto).ToList(),
            document.Fields.Select(ToDto).ToList());

    private static AnalyzedPageDto ToDto(DocumentPage page) =>
        new(page.Number.Value, page.Content, page.LineCount, page.WordCount);

    private static AnalyzedTableDto ToDto(DocumentTable table) =>
        new(
            table.RowCount,
            table.ColumnCount,
            table.Page?.Value,
            table.Cells.Select(ToDto).ToList());

    private static AnalyzedTableCellDto ToDto(DocumentTableCell cell) =>
        new(cell.RowIndex, cell.ColumnIndex, cell.Content, cell.IsHeader);

    private static ExtractedFieldDto ToDto(ExtractedField field) =>
        new(field.Name, field.Value, field.Confidence.Value, field.Page?.Value);
}
