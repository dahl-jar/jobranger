using System.Diagnostics;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Tests.Text;

public sealed class HtmlEntitiesTests {
    private const int longNameLength = 200_000;
    private static readonly TimeSpan decodeBudget = TimeSpan.FromSeconds(1);

    [Theory]
    [InlineData("&NotNestedLessLess;", "\u2AA1\u0338")]
    [InlineData("&AMP;", "&")]
    public void ShouldDecodeEntity_WhenNameIsHtml5Only(string text, string expected) {
        Assert.Equal(expected, HtmlEntities.Decode(text));
    }

    [Theory]
    [InlineData("&ampx", "&x")]
    [InlineData("&notit;", "\u00ACit;")]
    public void ShouldDecodeLongestLegacyName_WhenSemicolonIsMissing(string text, string expected) {
        Assert.Equal(expected, HtmlEntities.Decode(text));
    }

    [Theory]
    [InlineData("&zzz;")]
    [InlineData("&Amp;")]
    [InlineData("& amp;")]
    [InlineData("&")]
    public void ShouldKeepText_WhenNamedReferenceIsUnknown(string text) {
        Assert.Equal(text, HtmlEntities.Decode(text));
    }

    [Theory]
    [InlineData("&#x;")]
    [InlineData("&#;")]
    [InlineData("&#")]
    [InlineData("a&#")]
    public void ShouldKeepText_WhenNumericReferenceHasNoDigits(string text) {
        Assert.Equal(text, HtmlEntities.Decode(text));
    }

    [Fact]
    public void ShouldDecodeWithinOneSecond_WhenNameRunIsLong() {
        string text = "&" + new string('a', longNameLength);
        Stopwatch watch = Stopwatch.StartNew();

        string decoded = HtmlEntities.Decode(text);

        Assert.Equal(text, decoded);
        Assert.True(watch.Elapsed < decodeBudget, $"decode took {watch.Elapsed}");
    }

    [Theory]
    [InlineData("&#65;", "A")]
    [InlineData("&#0000065;", "A")]
    [InlineData("&#x41", "A")]
    [InlineData("&#X41;", "A")]
    [InlineData("&#x1F600;", "\U0001F600")]
    [InlineData("&#65bc", "Abc")]
    public void ShouldDecodeNumericReference_WhenCodeIsValid(string text, string expected) {
        Assert.Equal(expected, HtmlEntities.Decode(text));
    }

    [Theory]
    [InlineData("&#128;", "\u20AC")]
    [InlineData("&#13;", "\r")]
    [InlineData("&#0;", "\uFFFD")]
    public void ShouldMapReference_WhenCodeIsInPythonCharrefTable(string text, string expected) {
        Assert.Equal(expected, HtmlEntities.Decode(text));
    }

    [Theory]
    [InlineData("&#xD800;")]
    [InlineData("&#xDFFF;")]
    [InlineData("&#x110000;")]
    [InlineData("&#99999999999999999999;")]
    public void ShouldReplaceReference_WhenCodeIsSurrogateOrTooLarge(string text) {
        Assert.Equal("\uFFFD", HtmlEntities.Decode(text));
    }

    [Theory]
    [InlineData("&#1;")]
    [InlineData("&#127;")]
    [InlineData("&#xFFFE;")]
    public void ShouldDropReference_WhenCodeIsNoncharacterOrControl(string text) {
        Assert.Equal(string.Empty, HtmlEntities.Decode(text));
    }
}
