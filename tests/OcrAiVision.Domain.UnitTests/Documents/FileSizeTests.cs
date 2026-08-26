using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class FileSizeTests
{
    [Fact]
    public void FromMegabytes_converts_to_bytes()
    {
        Assert.Equal(2 * 1024 * 1024, FileSize.FromMegabytes(2).Bytes);
    }

    [Fact]
    public void FromBytes_rejects_a_negative_size()
    {
        Assert.Throws<DomainException>(() => FileSize.FromBytes(-1));
    }

    [Fact]
    public void A_zero_byte_size_is_empty()
    {
        Assert.True(FileSize.FromBytes(0).IsEmpty);
        Assert.False(FileSize.FromBytes(1).IsEmpty);
    }

    [Fact]
    public void Sizes_compare_by_byte_count()
    {
        var small = FileSize.FromBytes(10);
        var large = FileSize.FromBytes(11);

        Assert.True(large > small);
        Assert.True(small < large);
        Assert.True(large >= FileSize.FromBytes(11));
        Assert.True(small <= FileSize.FromBytes(10));
    }
}
