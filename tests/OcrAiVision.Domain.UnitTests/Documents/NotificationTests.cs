using OcrAiVision.Domain.Abstractions;
using Xunit;

namespace OcrAiVision.Domain.UnitTests.Documents;

public sealed class NotificationTests
{
    [Fact]
    public void A_new_notification_is_valid()
    {
        var notification = new Notification();

        Assert.True(notification.IsValid);
        Assert.False(notification.HasErrors);
    }

    [Fact]
    public void Errors_are_kept_in_the_order_they_were_added()
    {
        var notification = new Notification()
            .AddError("first")
            .AddError("second");

        Assert.Equal(["first", "second"], notification.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_messages_are_ignored(string message)
    {
        Assert.True(new Notification().AddError(message).IsValid);
    }

    [Fact]
    public void AddErrorIf_records_only_when_the_condition_holds()
    {
        var notification = new Notification()
            .AddErrorIf(condition: false, "skipped")
            .AddErrorIf(condition: true, "recorded");

        Assert.Equal(["recorded"], notification.Errors);
    }

    [Fact]
    public void ToString_joins_the_recorded_errors()
    {
        var notification = new Notification().AddError("one.").AddError("two.");

        Assert.Equal("one. two.", notification.ToString());
    }
}
