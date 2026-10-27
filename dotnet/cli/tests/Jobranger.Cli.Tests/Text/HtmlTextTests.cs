using System.Diagnostics;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Text;

public sealed class HtmlTextTests {
    private const int bracketCount = 200_000;
    private static readonly TimeSpan stripBudget = TimeSpan.FromSeconds(1);

    [Theory]
    [InlineData("<p>Hei</p><p>der</p>", "Hei der")]
    [InlineData("1 < 2 > 0", "1 0")]
    public void ShouldReplaceTagsWithSpace_WhenHtmlHasTags(string html, string expected) {
        Assert.Equal(expected, HtmlText.Strip(html));
    }

    [Fact]
    public void ShouldKeepDecodedBrackets_WhenEntitiesSpellATag() {
        Assert.Equal("a<b>c", HtmlText.Strip("a&lt;b&gt;c"));
    }

    [Theory]
    [InlineData("a < b", "a < b")]
    [InlineData("a <> b", "a <> b")]
    [InlineData("<b>a</b> <> b", "a <> b")]
    public void ShouldKeepBracket_WhenItOpensNoTag(string html, string expected) {
        Assert.Equal(expected, HtmlText.Strip(html));
    }

    [Theory]
    [InlineData("a&nbsp;&nbsp;b", "a b")]
    [InlineData("a\u001cb", "a b")]
    [InlineData("  a \n b  ", "a b")]
    [InlineData("a\u180Eb", "a\u180Eb")]
    public void ShouldCollapseWhitespace_WhenPythonCountsItAsSpace(string html, string expected) {
        Assert.Equal(expected, HtmlText.Strip(html));
    }

    [Fact]
    public void ShouldStripWithinOneSecond_WhenTextHasManyOpenBrackets() {
        string html = new('<', bracketCount);
        Stopwatch watch = Stopwatch.StartNew();

        string text = HtmlText.Strip(html);

        Assert.Equal(html, text);
        Assert.True(watch.Elapsed < stripBudget, $"strip took {watch.Elapsed}");
    }
}
