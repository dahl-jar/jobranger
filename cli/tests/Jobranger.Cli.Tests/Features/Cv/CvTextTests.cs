using System.Text;
using Jobranger.Cli.Features.Cv;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Cv;

public sealed class CvTextTests {
    private const int cp1252 = 1252;

    private static readonly string norwegianLine = string.Concat(Enumerable.Repeat("Utvikler i Bergen og Ålesund. Ærlig, øvet og våken. ", 6));

    private static readonly Encoding windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(cp1252)!;

    public static TheoryData<byte[], string> DecodedFiles => new() {
        { Encoding.UTF8.GetBytes(norwegianLine), norwegianLine },
        { [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(norwegianLine)], norwegianLine },
        { [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(norwegianLine)], norwegianLine },
        { [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(norwegianLine)], norwegianLine },
        { windows1252.GetBytes(norwegianLine), norwegianLine },
        { [0xEF, 0xBB, 0xBF, .. windows1252.GetBytes(norwegianLine)], norwegianLine },
        { windows1252.GetBytes(norwegianLine + "«SKOLEN PÅ»"), norwegianLine + "«SKOLEN PÅ»" },
        { [.. Encoding.UTF8.GetBytes(norwegianLine), 0xFF], norwegianLine },
        { [.. Encoding.UTF8.GetBytes(CvFixtures.LongLine), 0xC3, 0xA5, 0x7F, 0xFF], CvFixtures.LongLine + "Ã¥ÿ" },
        { [.. Encoding.UTF8.GetBytes(CvFixtures.LongLine), 0x81], CvFixtures.LongLine },
    };

    public static TheoryData<byte[]> BrokenFiles => new() {
        { [.. Encoding.UTF8.GetBytes(CvFixtures.LongLine), 0x00, 0x01, 0x02] },
        { [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(norwegianLine), 0x78] },
        { [.. "\n  "u8, .. CvFixtures.Pdf([CvFixtures.PlainStream(CvFixtures.PdfTextLine)])] },
        { CvFixtures.Pdf([CvFixtures.PlainStream(CvFixtures.PdfTextLine)]) },
    };

    public static TheoryData<string> UnsafeTexts => new() {
        "\u0001\u0007\u001B\u001F",
        "\u007F\u0085\u009B",
        "\u061C\u200E\u202E\u2066\u200B\uFEFF\u00AD",
        "\U000E0001\U000E0041",
        "\uD800",
    };

    [Theory]
    [MemberData(nameof(DecodedFiles))]
    public void ShouldDecodeTextFile_WhenEncodingIsKnown(byte[] data, string expected) {
        string text = CvText.FromTextFile(data);

        Assert.Equal(expected, text);
    }

    [Theory]
    [MemberData(nameof(BrokenFiles))]
    public void ShouldThrowUnreadable_WhenTextFileIsBinary(byte[] data) {
        CvUnreadableException error = Assert.Throws<CvUnreadableException>(() => CvText.FromTextFile(data));

        Assert.Equal(CvUnreadableException.Unreadable, error.Reason);
    }

    [Theory]
    [InlineData("\U0001F468\u200D\U0001F4BB")]
    [InlineData("می\u200Cخواهم")]
    public void ShouldKeepJoiners_WhenTextUsesThem(string joined) {
        string text = CvText.Checked($"{CvFixtures.LongLine} {joined}");

        Assert.Equal($"{CvFixtures.LongLine} {joined}", text);
    }

    [Theory]
    [MemberData(nameof(UnsafeTexts))]
    public void ShouldRemoveUnsafeCharacters_WhenTextHasThem(string unsafeText) {
        string text = CvText.Checked($"{CvFixtures.LongLine}\t{unsafeText}\nend");

        Assert.Equal($"{CvFixtures.LongLine}\t\nend", text);
    }

    [Fact]
    public void ShouldTurnCarriageReturnsIntoLineBreaks_WhenTextHasThem() {
        string text = CvText.Checked($"{CvFixtures.LongLine}\r\nLINE-TWO\rLINE-THREE");

        Assert.Equal($"{CvFixtures.LongLine}\nLINE-TWO\nLINE-THREE", text);
    }

    [Theory]
    [InlineData(199, CvUnreadableException.NoText)]
    [InlineData(200, null)]
    public void ShouldNeed200NonSpaceCharacters_WhenCheckingText(int letters, string? expected) {
        string content = new string('x', letters) + string.Concat(Enumerable.Repeat(" \n\t", 100));

        string? reason = CvFixtures.Reason(() => CvText.Checked(content));

        Assert.Equal(expected, reason);
    }

    [Theory]
    [InlineData(100_000, null)]
    [InlineData(100_001, CvUnreadableException.TooLarge)]
    public void ShouldRejectText_WhenOver100000Characters(int length, string? expected) {
        string content = new('x', length);

        string? reason = CvFixtures.Reason(() => CvText.Checked(content));

        Assert.Equal(expected, reason);
    }

    [Fact]
    public void ShouldCountCodePoints_WhenTextHasAstralCharacters() {
        string content = string.Concat(Enumerable.Repeat("\U0001F468", 100_000));

        string text = CvText.Checked(content);

        Assert.Equal(content, text);
    }
}
