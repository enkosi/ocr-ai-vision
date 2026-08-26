using OcrAiVision.Domain.Abstractions;
using OcrAiVision.Domain.Documents.ValueObjects;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class ConfidenceScoreTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(0.5d)]
    [InlineData(1d)]
    public void Create_accepts_values_within_the_unit_interval(double value)
    {
        var score = ConfidenceScore.Create(value);

        Assert.Equal(value, score.Value);
    }

    [Theory]
    [InlineData(-0.0001d)]
    [InlineData(1.0001d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Create_rejects_values_outside_the_unit_interval(double value)
    {
        Assert.Throws<DomainException>(() => ConfidenceScore.Create(value));
    }

    [Theory]
    [InlineData(0.884d, 88)]
    [InlineData(0.885d, 89)]
    [InlineData(1d, 100)]
    public void Percentage_rounds_away_from_zero(double value, int expected)
    {
        Assert.Equal(expected, ConfidenceScore.Create(value).Percentage);
    }

    [Fact]
    public void CreateOrMinimum_falls_back_when_the_model_reported_nothing()
    {
        Assert.Equal(ConfidenceScore.Minimum, ConfidenceScore.CreateOrMinimum(null));
    }

    [Fact]
    public void Scores_with_the_same_value_are_equal()
    {
        var first = ConfidenceScore.Create(0.42d);
        var second = ConfidenceScore.Create(0.42d);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Scores_with_different_values_are_not_equal()
    {
        Assert.NotEqual(ConfidenceScore.Create(0.42d), ConfidenceScore.Create(0.43d));
    }
}
