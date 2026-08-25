using System.Text;
using OcrAiVision.Domain.Documents;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class DocumentUploadPolicyTests
{
    private static readonly DocumentUploadPolicy Policy = new(FileSize.FromMegabytes(10));

    [Fact]
    public void A_supported_document_within_the_limit_passes()
    {
        var notification = Policy.Validate(UploadOf("invoice.pdf", "application/pdf", 4_096));

        Assert.True(notification.IsValid);
        Assert.Empty(notification.Errors);
    }

    [Fact]
    public void An_empty_document_is_rejected()
    {
        var notification = Policy.Validate(UploadOf("empty.pdf", "application/pdf", 0));

        Assert.True(notification.HasErrors);
        Assert.Contains(notification.Errors, error => error.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_document_over_the_limit_is_rejected()
    {
        var notification = Policy.Validate(
            UploadOf("huge.pdf", "application/pdf", FileSize.FromMegabytes(11).Bytes));

        Assert.Contains(notification.Errors, error => error.Contains("exceeds", StringComparison.Ordinal));
    }

    [Fact]
    public void An_unsupported_media_type_is_rejected()
    {
        var notification = Policy.Validate(UploadOf("archive.zip", "application/zip", 1_024));

        Assert.Contains(notification.Errors, error => error.Contains("not a supported", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_violation_is_reported_rather_than_only_the_first()
    {
        var notification = Policy.Validate(UploadOf("archive.zip", "application/zip", 0));

        Assert.Equal(2, notification.Errors.Count);
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/tiff")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public void The_formats_document_intelligence_reads_are_accepted(string mediaType)
    {
        Assert.True(Policy.Validate(UploadOf("file", mediaType, 512)).IsValid);
    }

    [Fact]
    public void The_default_limit_applies_when_none_is_configured()
    {
        Assert.Equal(DocumentUploadPolicy.DefaultMaximumSize, new DocumentUploadPolicy().MaximumSize);
    }

    private static DocumentUpload UploadOf(string fileName, string mediaType, long sizeInBytes) =>
        DocumentUpload.Create(
            fileName,
            DocumentMediaType.Create(mediaType),
            FileSize.FromBytes(sizeInBytes),
            () => new MemoryStream(Encoding.UTF8.GetBytes("content")));
}
