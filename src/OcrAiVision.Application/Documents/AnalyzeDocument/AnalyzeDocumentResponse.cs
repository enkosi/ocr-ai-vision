namespace OcrAiVision.Application.Documents.AnalyzeDocument;

/// <summary>
/// The output boundary of the analyse-document use case: a flat, serialisable
/// view of the analysis. Keeping this separate from the domain aggregate means
/// the wire contract can stay stable while the domain model evolves.
/// </summary>
/// <param name="FileName">The file that was analysed.</param>
/// <param name="ModelId">The model that produced the result.</param>
/// <param name="Content">The full text of the document.</param>
/// <param name="PageCount">How many pages were read.</param>
/// <param name="LowestFieldConfidence">The weakest field confidence, or <see langword="null"/> when no fields were extracted.</param>
/// <param name="Pages">The recognised pages.</param>
/// <param name="Tables">The recognised tables.</param>
/// <param name="Fields">The labelled values that were extracted.</param>
public sealed record AnalyzeDocumentResponse(
    string FileName,
    string ModelId,
    string Content,
    int PageCount,
    double? LowestFieldConfidence,
    IReadOnlyList<AnalyzedPageDto> Pages,
    IReadOnlyList<AnalyzedTableDto> Tables,
    IReadOnlyList<ExtractedFieldDto> Fields);

/// <summary>A page of the analysed document.</summary>
/// <param name="Number">The one-based page ordinal.</param>
/// <param name="Content">The text recovered from the page.</param>
/// <param name="LineCount">How many lines were detected.</param>
/// <param name="WordCount">How many words were detected.</param>
public sealed record AnalyzedPageDto(int Number, string Content, int LineCount, int WordCount);

/// <summary>A table recognised in the analysed document.</summary>
/// <param name="RowCount">How many rows the table has.</param>
/// <param name="ColumnCount">How many columns the table has.</param>
/// <param name="Page">The page the table starts on, when known.</param>
/// <param name="Cells">The recognised cells.</param>
public sealed record AnalyzedTableDto(
    int RowCount,
    int ColumnCount,
    int? Page,
    IReadOnlyList<AnalyzedTableCellDto> Cells);

/// <summary>A single table cell.</summary>
/// <param name="RowIndex">The zero-based row index.</param>
/// <param name="ColumnIndex">The zero-based column index.</param>
/// <param name="Content">The text within the cell.</param>
/// <param name="IsHeader">Whether the cell was classified as a header.</param>
public sealed record AnalyzedTableCellDto(int RowIndex, int ColumnIndex, string Content, bool IsHeader);

/// <summary>A labelled value extracted from the document.</summary>
/// <param name="Name">The field label.</param>
/// <param name="Value">The recovered value, when there was one.</param>
/// <param name="Confidence">The model's confidence, between 0 and 1.</param>
/// <param name="Page">The page the field was found on, when known.</param>
public sealed record ExtractedFieldDto(string Name, string? Value, double Confidence, int? Page);
