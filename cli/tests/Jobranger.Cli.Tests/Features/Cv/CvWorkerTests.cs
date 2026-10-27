using System.Text;
using Jobranger.Cli.Features.Cv;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Cv;

public sealed class CvWorkerTests {
    private const string docx = CvWorker.DocxSuffix;
    private const string pdf = CvWorker.PdfSuffix;

    public static TheoryData<string, byte[], string> FailedFiles => new() {
        {
            docx,
            CvFixtures.Docx(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(CvFixtures.Document(CvFixtures.Paragraph(CvFixtures.LongLine)))
                .Replace("?>", "?><!DOCTYPE w:document>", StringComparison.Ordinal))),
            CvUnreadableException.Unreadable
        },
        {
            docx,
            CvFixtures.Docx(null, ("word/header1.xml", CvFixtures.WordPart("hdr", CvFixtures.Paragraph(CvFixtures.LongLine)))),
            CvUnreadableException.Unreadable
        },
        { pdf, "not a pdf"u8.ToArray(), CvUnreadableException.Unreadable },
        { ".doc", CvFixtures.Docx(CvFixtures.Document(CvFixtures.Paragraph(CvFixtures.LongLine))), CvUnreadableException.Unreadable },
        {
            pdf,
            CvFixtures.Pdf([.. Enumerable.Range(0, PdfText.MaxPages + 1).Select(_ => CvFixtures.PdfPage("PAGE"))]),
            CvUnreadableException.TooLarge
        },
    };

    [Theory]
    [MemberData(nameof(FailedFiles))]
    public void ShouldReplyReason_WhenExtractionFails(string suffix, byte[] data, string reason) {
        byte[] reply = CvWorker.Reply(suffix, data);

        Assert.Equal($"{{\"reason\":\"{reason}\"}}", Encoding.ASCII.GetString(reply));
    }

    [Fact]
    public void ShouldDropLoneSurrogate_WhenWritingReply() {
        byte[] reply = CvWorker.TextReply("Backend\uD800 developer");

        Assert.Equal("{\"text\":\"Backend developer\"}", Encoding.Latin1.GetString(reply));
    }
}
