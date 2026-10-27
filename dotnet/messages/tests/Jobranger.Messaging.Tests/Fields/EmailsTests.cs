using Jobranger.Messaging.Fields;

namespace Jobranger.Messaging.Tests.Fields;

public sealed class EmailsTests {
    [Theory]
    [InlineData("user@example.test", true)]
    [InlineData("user@example", false)]
    [InlineData("user@example.test\n", false)]
    public void ShouldMatchEmailPattern_WhenTextIsShort(string text, bool valid) {
        Assert.Equal(valid, Emails.IsValid(text));
    }
}
