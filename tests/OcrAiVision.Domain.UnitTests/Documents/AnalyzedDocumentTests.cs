using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class AnalyzedDocumentTests
{
    [Fact]
    public void Pages_are_ordered_by_page_number_regardless_of_the_order_supplied()
    {
        var document = AnalyzedDocument.Create(
            "scan.pdf",
            ModelId.PrebuiltLayout,
            "text",
            pages:
            [
                Page(3),
                Page(1),
                Page(2),
            ]);

        Assert.Equal([1, 2, 3], document.Pages.Select(page => page.Number.Value));
        Assert.Equal(3, document.PageCount);
    }

    [Fact]
    public void A_repeated_page_number_is_rejected()
    {
        Assert.Throws<DomainException>(() => AnalyzedDocument.Create(
            "scan.pdf",
            ModelId.PrebuiltLayout,
            "text",
            pages: [Page(1), Page(1)]));
    }

    [Fact]
    public void Create_rejects_a_missing_source_file_name()
    {
        Assert.Throws<DomainException>(() =>
            AnalyzedDocument.Create("  ", ModelId.PrebuiltLayout, "text"));
    }

    [Fact]
    public void A_null_content_becomes_an_empty_string()
    {
        var document = AnalyzedDocument.Create("scan.pdf", ModelId.PrebuiltLayout, null);

        Assert.Equal(string.Empty, document.Content);
        Assert.False(document.HasContent);
    }

    [Fact]
    public void LowestFieldConfidence_reports_the_weakest_field()
    {
        var document = AnalyzedDocument.Create(
            "invoice.pdf",
            ModelId.Create("prebuilt-invoice"),
            "text",
            fields:
            [
                Field("Total", 0.97d),
                Field("VatNumber", 0.42d),
                Field("Date", 0.88d),
            ]);

        Assert.Equal(0.42d, document.LowestFieldConfidence()!.Value);
    }

    [Fact]
    public void LowestFieldConfidence_is_absent_when_no_fields_were_extracted()
    {
        var document = AnalyzedDocument.Create("scan.pdf", ModelId.PrebuiltLayout, "text");

        Assert.Null(document.LowestFieldConfidence());
    }

    [Fact]
    public void MeetsConfidenceThreshold_fails_when_any_field_is_below_it()
    {
        var document = AnalyzedDocument.Create(
            "invoice.pdf",
            ModelId.Create("prebuilt-invoice"),
            "text",
            fields: [Field("Total", 0.97d), Field("VatNumber", 0.42d)]);

        Assert.False(document.MeetsConfidenceThreshold(ConfidenceScore.Create(0.8d)));
        Assert.True(document.MeetsConfidenceThreshold(ConfidenceScore.Create(0.4d)));
    }

    [Fact]
    public void A_document_with_no_fields_meets_any_threshold()
    {
        var document = AnalyzedDocument.Create("scan.pdf", ModelId.PrebuiltLayout, "text");

        Assert.True(document.MeetsConfidenceThreshold(ConfidenceScore.Create(1d)));
    }

    private static DocumentPage Page(int number) =>
        DocumentPage.Create(PageNumber.Create(number), $"page {number}", 1, 2);

    private static ExtractedField Field(string name, double confidence) =>
        ExtractedField.Create(name, "value", ConfidenceScore.Create(confidence));
}
