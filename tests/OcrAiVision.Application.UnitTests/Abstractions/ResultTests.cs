using OcrAiVision.Application.Abstractions;
using Xunit;

namespace OcrAiVision.Application.UnitTests.Abstractions;

public sealed class ResultTests
{
    [Fact]
    public void A_success_carries_its_value()
    {
        var result = Result<int>.Success(7);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public void A_failure_carries_its_error()
    {
        var error = Error.Validation("code", "message");
        var result = Result<int>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Reading_the_value_of_a_failure_is_a_programmer_error()
    {
        var result = Result<int>.Failure(Error.Dependency("code", "message"));

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Reading_the_error_of_a_success_is_a_programmer_error()
    {
        Assert.Throws<InvalidOperationException>(() => Result<int>.Success(1).Error);
    }

    [Fact]
    public void Failure_rejects_a_null_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!));
    }

    [Fact]
    public void Match_applies_the_branch_that_fits_the_outcome()
    {
        Assert.Equal("ok:7", Result<int>.Success(7).Match(value => $"ok:{value}", error => $"err:{error.Code}"));

        Assert.Equal(
            "err:boom",
            Result<int>.Failure(Error.Timeout("boom", "message")).Match(value => $"ok:{value}", error => $"err:{error.Code}"));
    }

    [Fact]
    public void A_value_converts_implicitly_into_a_success()
    {
        Result<string> result = "done";

        Assert.True(result.IsSuccess);
        Assert.Equal("done", result.Value);
    }

    [Fact]
    public void An_error_converts_implicitly_into_a_failure()
    {
        Result<string> result = Error.Validation("code", "message");

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData(ErrorKind.Validation)]
    [InlineData(ErrorKind.Dependency)]
    [InlineData(ErrorKind.Timeout)]
    [InlineData(ErrorKind.Cancelled)]
    public void Every_error_kind_survives_a_round_trip(ErrorKind kind)
    {
        var error = new Error(kind, "code", "message");

        Assert.Equal(kind, Result<int>.Failure(error).Error.Kind);
    }
}
