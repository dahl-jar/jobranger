using Jobranger.Cli.Features.Digest;

namespace Jobranger.Cli.Tests.Features.Digest;

public sealed class CardTextTests {
    private const string text = "abc";

    [Fact]
    public void ShouldKeepText_WhenTextIsAtLimit() {
        string cut = CardText.Cut(text, text.Length);

        Assert.Equal(text, cut);
    }
}
