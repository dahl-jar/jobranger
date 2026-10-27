using System.Text;
using Jobranger.Cli.Features.Cv;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Cv;

public sealed class PdfTextTests {
    [Fact]
    public void ShouldJoinPagesWithLineBreaks_WhenPdfHasTwoPages() {
        byte[] data = CvFixtures.Pdf([CvFixtures.PdfPage("ONE"), CvFixtures.PdfPage("TWO")]);

        string text = PdfText.Extract(data);

        Assert.Equal("ONE\nTWO", text);
    }

    [Fact]
    public void ShouldDropTrailingLineBreak_WhenLastPageIsEmpty() {
        byte[] data = CvFixtures.Pdf([CvFixtures.PdfPage("ONE"), CvFixtures.PdfPage("")]);

        string text = PdfText.Extract(data);

        Assert.Equal("ONE", text);
    }

    [Theory]
    [InlineData(PdfText.MaxPages, null)]
    [InlineData(PdfText.MaxPages + 1, CvUnreadableException.TooLarge)]
    public void ShouldThrowTooLarge_WhenPdfHasFiftyOnePages(int pages, string? expected) {
        byte[] data = CvFixtures.Pdf([.. Enumerable.Range(0, pages).Select(_ => CvFixtures.PdfPage("PAGE"))]);

        string? reason = CvFixtures.Reason(() => PdfText.Extract(data));

        Assert.Equal(expected, reason);
    }

    [Fact]
    public void ShouldStopReadingPages_WhenTextPassesCap() {
        byte[] brokenPage = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(
            CvFixtures.FlateStream(Encoding.Latin1.GetBytes(CvFixtures.PdfTextLine))).Replace("/FlateDecode", "/FlateDekode", StringComparison.Ordinal));
        byte[] data = CvFixtures.Pdf([CvFixtures.PdfPage(new string('x', CvText.MaxChars + 1)), brokenPage]);

        string? reason = CvFixtures.Reason(() => PdfText.Extract(data));

        Assert.Equal(CvUnreadableException.TooLarge, reason);
    }
}
