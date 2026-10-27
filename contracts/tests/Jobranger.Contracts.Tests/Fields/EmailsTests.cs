using Jobranger.Contracts.Fields;

namespace Jobranger.Contracts.Tests.Fields;

public sealed class EmailsTests {
    [Theory]
    [InlineData("user@example.test", true)]
    [InlineData("user@example", false)]
    [InlineData("user@example.test\n", false)]
    public void ShouldMatchEmailPattern_WhenTextIsShort(string text, bool valid) {
        Assert.Equal(valid, Emails.IsValid(text));
    }
}
