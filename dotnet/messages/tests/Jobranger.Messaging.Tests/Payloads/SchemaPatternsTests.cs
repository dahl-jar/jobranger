using System.Text.RegularExpressions;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Tests.Payloads;

public sealed class SchemaPatternsTests {
    [Theory]
    [InlineData("^ab$", "ab\n", false)]
    [InlineData(@"^a\$$", "a$", true)]
    [InlineData(@"^a\[b$", "a[b", true)]
    [InlineData(@"^[\x21-\x7e]+$", "ab", true)]
    [InlineData("^\U0001BCA0+$", "\U0001BCA0\U0001BCA0", true)]
    [InlineData("^[\U0001BCA0-\U0001BCA3]$", "\U0001BCA1", true)]
    [InlineData("^[\U0001BCA0-\U0001BCA3]$", "\U0001BCA4", false)]
    [InlineData("^[\U000E0000-\U000E0FFF]$", "\U000E0800", true)]
    [InlineData("^[\U0001F3FF-\U0001F400]$", "\U0001F400", true)]
    [InlineData("^[\U0001F3FF-\U0001F400]$", "\U0001F3FF", true)]
    [InlineData("^[a\U0001BCA0]$", "a", true)]
    [InlineData(@"^[\s" + "\U0001BCA0]$", " ", true)]
    [InlineData(@"^[\x41" + "\U0001BCA0]$", "A", true)]
    [InlineData(@"^[\ud82f\udca0]$", "\U0001BCA0", true)]
    [InlineData("^[\U0001BCA0-]$", "-", true)]
    [InlineData("^[\uFFF0-\U00010001]$", "\uFFF5", true)]
    [InlineData("^[\uFFF0-\U00010001]$", "\U00010000", true)]
    [InlineData("^[^a\U0001BCA0-\U0001BCA3]$", "\U0001BCA1", false)]
    [InlineData("^[^a\U0001BCA0-\U0001BCA3]$", "a", false)]
    [InlineData("^[^a\U0001BCA0-\U0001BCA3]$", "b", true)]
    [InlineData("^[^a\U0001BCA0-\U0001BCA3]$", "\U0001F600", true)]
    [InlineData("^[^a\U0001BCA0-\U0001BCA3]$", "^", true)]
    [InlineData(@"^[" + "\U0001BCA0" + @"\s]$", " ", true)]
    [InlineData(@"^[\u0041\u0042]$", "B", true)]
    [InlineData("^[\U000E0000-\U000E0FFF]$", "\U000DFC00", false)]
    [InlineData("^[\U000E0000-\U000E0FFF]$", "\U000E1000", false)]
    [InlineData(@"^[\ud82f\udca0]$", "2", false)]
    public void ShouldMatchLikeUnicodeJsRegex_WhenPatternIsTranslated(string pattern, string input, bool matches) {
        Regex translated = new(SchemaPatterns.ToDotNet(pattern), RegexOptions.None, TimeSpan.FromSeconds(1));

        Assert.Equal(matches, translated.IsMatch(input));
    }
}
