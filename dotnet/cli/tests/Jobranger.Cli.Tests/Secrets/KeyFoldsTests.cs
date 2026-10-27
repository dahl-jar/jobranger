using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Tests.Secrets;

public sealed class KeyFoldsTests {
    [Theory]
    [InlineData("Ｓｋ－１", "Sk-1")]
    [InlineData("\U0001D400\U0001D41A", "Aa")]
    [InlineData("ﬁx", "fix")]
    public void ShouldFoldToAscii_WhenCharHasCompatibilityForm(string text, string expected) {
        string folded = KeyFolds.Fold(text);

        Assert.Equal(expected, folded);
    }

    [Fact]
    public void ShouldKeepChar_WhenItHasNoAsciiFold() {
        string folded = KeyFolds.Fold("æ\U0001F600");

        Assert.Equal("æ\U0001F600", folded);
    }
}
