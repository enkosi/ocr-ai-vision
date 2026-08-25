using System.Text;
using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class DocumentUploadTests
{
    [Fact]
    public void Create_trims_the_file_name()
    {
        var upload = Build("  scan.pdf  ");

        Assert.Equal("scan.pdf", upload.FileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_file_name(string fileName)
    {
        Assert.Throws<DomainException>(() => Build(fileName));
    }

    [Fact]
    public void OpenReadStream_yields_a_fresh_stream_on_every_call()
    {
        var upload = Build("scan.pdf");

        using var first = upload.OpenReadStream();
        using var second = upload.OpenReadStream();

        Assert.NotSame(first, second);
        Assert.Equal(0, first.Position);
        Assert.Equal(0, second.Position);
    }

    private static DocumentUpload Build(string fileName) =>
        DocumentUpload.Create(
            fileName,
            DocumentMediaType.Create("application/pdf"),
            FileSize.FromBytes(7),
            () => new MemoryStream(Encoding.UTF8.GetBytes("content")));
}
