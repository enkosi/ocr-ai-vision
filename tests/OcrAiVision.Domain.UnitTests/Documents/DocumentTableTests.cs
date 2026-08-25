using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class DocumentTableTests
{
    [Fact]
    public void A_table_keeps_the_cells_it_was_given()
    {
        var table = DocumentTable.Create(
            rowCount: 2,
            columnCount: 2,
            cells:
            [
                DocumentTableCell.Create(0, 0, "Item", isHeader: true),
                DocumentTableCell.Create(1, 0, "Widget"),
            ],
            page: PageNumber.Create(1));

        Assert.Equal(2, table.Cells.Count);
        Assert.True(table.Cells[0].IsHeader);
        Assert.Equal(1, table.Page!.Value);
    }

    [Fact]
    public void A_cell_outside_the_declared_grid_is_rejected()
    {
        Assert.Throws<DomainException>(() => DocumentTable.Create(
            rowCount: 1,
            columnCount: 1,
            cells: [DocumentTableCell.Create(0, 5, "off the edge")]));
    }

    [Fact]
    public void A_cell_with_a_negative_index_is_rejected()
    {
        Assert.Throws<DomainException>(() => DocumentTableCell.Create(-1, 0, "content"));
    }

    [Fact]
    public void A_null_cell_content_becomes_an_empty_string()
    {
        Assert.Equal(string.Empty, DocumentTableCell.Create(0, 0, null).Content);
    }
}
