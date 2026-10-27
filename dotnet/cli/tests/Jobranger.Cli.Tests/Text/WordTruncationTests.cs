using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Text;

public sealed class WordTruncationTests {
    private const int limit = 160;
    private const string ellipsis = "\u2026";
    private const string emoji = "\U0001F600";

    [Fact]
    public void ShouldKeepText_WhenItFitsLimit() {
        string text = new('x', limit);

        Assert.Equal(text, WordTruncation.Cut(text, limit));
    }

    [Fact]
    public void ShouldCutAtLastWord_WhenTextIsOverLimit() {
        string text = string.Concat(Enumerable.Repeat("word ", 32)) + "xxxx";

        string cut = WordTruncation.Cut(text, limit);

        Assert.Equal(string.Concat(Enumerable.Repeat("word ", 31)).TrimEnd() + ellipsis, cut);
    }

    [Theory]
    [InlineData("alpha, ", "alpha")]
    [InlineData("alpha -;: ", "alpha")]
    public void ShouldDropTrailingPunctuation_WhenCutEndsInIt(string start, string expected) {
        string text = start + new string('b', 170);

        Assert.Equal(expected + ellipsis, WordTruncation.Cut(text, limit));
    }

    [Theory]
    [InlineData('x')]
    [InlineData(',')]
    public void ShouldCutMidWord_WhenNoWordFits(char filler) {
        string text = new(filler, limit + 1);

        Assert.Equal(new string(filler, limit - 1) + ellipsis, WordTruncation.Cut(text, limit));
    }

    [Fact]
    public void ShouldCountCodePoints_WhenTextHasAstralCharacters() {
        string text = string.Concat(Enumerable.Repeat(emoji, limit));

        Assert.Equal(text, WordTruncation.Cut(text, limit));
    }

    [Theory]
    [InlineData("a ", 170, "a", 0)]
    [InlineData("", 161, "", 159)]
    public void ShouldKeepSurrogatePairs_WhenCutIsInAstralText(
        string start, int emojiCount, string expectedStart, int expectedEmojiCount) {
        string text = start + string.Concat(Enumerable.Repeat(emoji, emojiCount));

        string cut = WordTruncation.Cut(text, limit);

        Assert.Equal(expectedStart + string.Concat(Enumerable.Repeat(emoji, expectedEmojiCount)) + ellipsis, cut);
    }

    [Fact]
    public void ShouldSplitWords_WhenPythonCountsSeparatorAsSpace() {
        Assert.Equal("alpha" + ellipsis, WordTruncation.Cut("alpha\u001cbeta gamma", 12));
    }

    [Fact]
    public void ShouldDropLeadingSpace_WhenCutPartIsOneWord() {
        Assert.Equal("alp" + ellipsis, WordTruncation.Cut("  alphabet", 6));
    }

    [Fact]
    public void ShouldKeepSpacePrefix_WhenCutPartHasNoWord() {
        Assert.Equal("  " + ellipsis, WordTruncation.Cut("     x", 3));
    }
}
