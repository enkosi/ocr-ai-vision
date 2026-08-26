using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class ModelIdTests
{
    [Fact]
    public void Create_trims_surrounding_whitespace()
    {
        Assert.Equal("prebuilt-invoice", ModelId.Create("  prebuilt-invoice  ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_identifiers(string value)
    {
        Assert.Throws<DomainException>(() => ModelId.Create(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void CreateOrDefault_falls_back_to_the_layout_model(string? value)
    {
        Assert.Equal(ModelId.PrebuiltLayout, ModelId.CreateOrDefault(value));
    }

    [Fact]
    public void CreateOrDefault_honours_a_supplied_model()
    {
        Assert.Equal("prebuilt-receipt", ModelId.CreateOrDefault("prebuilt-receipt").Value);
    }
}
