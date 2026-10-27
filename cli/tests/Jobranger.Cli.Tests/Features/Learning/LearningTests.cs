using LearningEntry = Jobranger.Cli.Features.Learning.Learning;

namespace Jobranger.Cli.Tests.Features.Learning;

public sealed class LearningTests {
    private const string title = "Utvikler";
    private const int descriptionLimit = 600;
    private const string rocket = "\U0001F680";

    [Theory]
    [InlineData(null)]
    [InlineData(" \n")]
    public void ShouldUseTitleOnly_WhenDescriptionIsBlank(string? description) {
        string text = LearningEntry.EmbedText(title, description);

        Assert.Equal(title, text);
    }

    [Fact]
    public void ShouldJoinTitleAndDescription_WhenDescriptionIsShort() {
        string text = LearningEntry.EmbedText(title, "Bergen");

        Assert.Equal(title + ". Bergen", text);
    }

    [Fact]
    public void ShouldCutDescriptionAt600_WhenDescriptionIsLonger() {
        string text = LearningEntry.EmbedText(title, new string('a', descriptionLimit + 1));

        Assert.Equal(title + ". " + new string('a', descriptionLimit), text);
    }

    [Fact]
    public void ShouldCutBeforeEmoji_WhenCutWouldSplitIt() {
        string text = LearningEntry.EmbedText(title, new string('a', descriptionLimit - 1) + rocket);

        Assert.Equal(title + ". " + new string('a', descriptionLimit - 1), text);
    }
}
