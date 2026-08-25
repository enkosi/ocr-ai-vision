using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class DocumentMediaTypeTests
{
    [Theory]
    [InlineData("application/pdf", "application/pdf")]
    [InlineData("APPLICATION/PDF", "application/pdf")]
    [InlineData("text/html; charset=utf-8", "text/html")]
    [InlineData("  image/png  ", "image/png")]
    public void Create_normalises_the_header_value(string header, string expected)
    {
        Assert.Equal(expected, DocumentMediaType.Create(header).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("; charset=utf-8")]
    public void Create_rejects_a_header_with_no_media_type(string header)
    {
        Assert.Throws<DomainException>(() => DocumentMediaType.Create(header));
    }
}
