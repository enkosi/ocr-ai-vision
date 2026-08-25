using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class PageNumberTests
{
    [Fact]
    public void Create_accepts_the_first_page()
    {
        Assert.Equal(1, PageNumber.Create(1).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Create_rejects_ordinals_below_one(int value)
    {
        Assert.Throws<DomainException>(() => PageNumber.Create(value));
    }

    [Fact]
    public void Pages_with_the_same_ordinal_are_equal()
    {
        Assert.Equal(PageNumber.Create(7), PageNumber.Create(7));
    }
}
