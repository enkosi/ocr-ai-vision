using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;

namespace OcrAiVision.Domain.Documents;

/// <summary>
/// A table the model recognised, flattened to its cell contents in row-major order.
/// </summary>
public sealed class DocumentTable
{
    private readonly List<DocumentTableCell> _cells;

    private DocumentTable(int rowCount, int columnCount, PageNumber? page, List<DocumentTableCell> cells)
    {
        RowCount = rowCount;
        ColumnCount = columnCount;
        Page = page;
        _cells = cells;
    }

    /// <summary>How many rows the table has.</summary>
    public int RowCount { get; }

    /// <summary>How many columns the table has.</summary>
    public int ColumnCount { get; }

    /// <summary>The page the table starts on, when the model reported one.</summary>
    public PageNumber? Page { get; }

    /// <summary>The recognised cells.</summary>
    public IReadOnlyList<DocumentTableCell> Cells => _cells;

    /// <summary>
    /// Creates a table, rejecting cells that fall outside the declared grid.
    /// </summary>
    public static DocumentTable Create(
        int rowCount,
        int columnCount,
        IEnumerable<DocumentTableCell> cells,
        PageNumber? page = null)
    {
        ArgumentNullException.ThrowIfNull(cells);

        if (rowCount < 0)
        {
            throw new DomainException($"Row count cannot be negative but was {rowCount}.");
        }

        if (columnCount < 0)
        {
            throw new DomainException($"Column count cannot be negative but was {columnCount}.");
        }

        var materialised = cells.ToList();

        foreach (var cell in materialised)
        {
            if (cell.RowIndex >= rowCount || cell.ColumnIndex >= columnCount)
            {
                throw new DomainException(
                    $"Cell ({cell.RowIndex}, {cell.ColumnIndex}) falls outside a " +
                    $"{rowCount}x{columnCount} table.");
            }
        }

        return new DocumentTable(rowCount, columnCount, page, materialised);
    }
}

/// <summary>
/// A single cell within a <see cref="DocumentTable"/>.
/// </summary>
public sealed class DocumentTableCell : ValueObject
{
    private DocumentTableCell(int rowIndex, int columnIndex, string content, bool isHeader)
    {
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        Content = content;
        IsHeader = isHeader;
    }

    /// <summary>The zero-based row index.</summary>
    public int RowIndex { get; }

    /// <summary>The zero-based column index.</summary>
    public int ColumnIndex { get; }

    /// <summary>The text within the cell.</summary>
    public string Content { get; }

    /// <summary>Whether the model classified this cell as a header.</summary>
    public bool IsHeader { get; }

    /// <summary>
    /// Creates a cell.
    /// </summary>
    public static DocumentTableCell Create(int rowIndex, int columnIndex, string? content, bool isHeader = false)
    {
        if (rowIndex < 0 || columnIndex < 0)
        {
            throw new DomainException(
                $"Cell indices cannot be negative but were ({rowIndex}, {columnIndex}).");
        }

        return new DocumentTableCell(rowIndex, columnIndex, content ?? string.Empty, isHeader);
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RowIndex;
        yield return ColumnIndex;
        yield return Content;
        yield return IsHeader;
    }
}
