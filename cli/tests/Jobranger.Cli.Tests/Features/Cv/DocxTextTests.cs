using System.Text;
using Jobranger.Cli.Features.Cv;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Cv;

public sealed class DocxTextTests {
    private const int halfTextCap = CvText.MaxChars / 2;
    private const int overHalfUnpackBudget = DocxText.MaxUnpackedBytes / 2 + 1;

    private static readonly string textBox =
        "<w:p><w:r><mc:AlternateContent><mc:Choice Requires=\"wps\"><w:drawing><wps:txbx><w:txbxContent>"
        + CvFixtures.Paragraph("TEXT-BOX")
        + "</w:txbxContent></wps:txbx></w:drawing></mc:Choice><mc:Fallback><w:pict><v:shape><v:textbox><w:txbxContent>"
        + CvFixtures.Paragraph("TEXT-BOX")
        + "</w:txbxContent></v:textbox></v:shape></w:pict></mc:Fallback></mc:AlternateContent></w:r></w:p>";

    public static TheoryData<string, string, int> StructureCases => new() {
        { textBox, "TEXT-BOX", 1 },
        { textBox + CvFixtures.Paragraph("AFTER-BOX"), "AFTER-BOX", 1 },
        {
            "<mc:AlternateContent><mc:Fallback>" + textBox + "</mc:Fallback></mc:AlternateContent>"
                + CvFixtures.Paragraph("AFTER-NESTED"),
            "AFTER-NESTED",
            1
        },
        { "<mc:AlternateContent><mc:Fallback/></mc:AlternateContent>" + CvFixtures.Paragraph("AFTER-EMPTY"), "AFTER-EMPTY", 1 },
        { "<w:p><w:del w:id=\"2\" w:author=\"user\"><w:r><w:delText>DELETED</w:delText></w:r></w:del></w:p>", "DELETED", 0 },
        { "<w:p><w:r><w:t>A</w:t><w:instrText>FIELD-CODE</w:instrText></w:r></w:p>", "FIELD-CODE", 0 },
        {
            "<w:p><w:r><w:t>LINE-ONE</w:t><w:br/><w:t>LINE-TWO</w:t><w:cr/><w:t>LINE-THREE</w:t><w:tab/><w:t>COLUMN</w:t></w:r></w:p>",
            "LINE-ONE\nLINE-TWO\nLINE-THREE\tCOLUMN",
            1
        },
    };

    [Theory]
    [MemberData(nameof(StructureCases))]
    public void ShouldExtractWordStructureText_WhenBodyHasIt(string body, string marker, int count) {
        byte[] data = CvFixtures.Docx(CvFixtures.Document(CvFixtures.Paragraph(CvFixtures.LongLine) + body));

        string text = DocxText.Extract(data);

        Assert.Equal(count, text.Split(marker).Length - 1);
    }

    [Fact]
    public void ShouldExtractText_WhenWordUsesStrictNamespace() {
        byte[] data = CvFixtures.Docx(CvFixtures.StrictDocument(
            "<w:p><w:r><w:t>e</w:t><w:noBreakHyphen/><w:t>post</w:t></w:r></w:p>" + CvFixtures.Paragraph("STRICT")));

        string text = DocxText.Extract(data);

        Assert.Equal("e-post\nSTRICT", text);
    }

    [Fact]
    public void ShouldSkipLineBreak_WhenRootFollowsDeclaration() {
        byte[] document = CvFixtures.Document(CvFixtures.Paragraph("BODY"));
        byte[] data = CvFixtures.Docx(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(document).Replace("?>", "?>\r\n", StringComparison.Ordinal)));

        string text = DocxText.Extract(data);

        Assert.Equal("BODY", text);
    }

    [Fact]
    public void ShouldSkipBreak_WhenElementIsNotWord() {
        byte[] data = CvFixtures.Docx(CvFixtures.Document(CvFixtures.Paragraph("ONE")
            + "<a:br xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\"/>" + CvFixtures.Paragraph("TWO")));

        string text = DocxText.Extract(data);

        Assert.Equal("ONE\nTWO", text);
    }

    [Fact]
    public void ShouldKeepBlankParagraphsAsLineBreaks_WhenBodyHasThem() {
        byte[] data = CvFixtures.Docx(CvFixtures.Document(
            CvFixtures.Paragraph("ONE") + "<w:p><w:r><w:t/></w:r></w:p>" + CvFixtures.Paragraph("TWO")));

        string text = DocxText.Extract(data);

        Assert.Equal("ONE\n\nTWO", text);
    }

    [Fact]
    public void ShouldReadPartsInPageOrder_WhenWordHasHeadersAndFooters() {
        (string, byte[])[] parts = [
            Part("footer", "ftr", 10), Part("footer", "ftr", 2), Part("footer", "ftr", 1),
            Part("header", "hdr", 10), Part("header", "hdr", 2), Part("header", "hdr", 1),
        ];
        byte[] data = CvFixtures.Docx(CvFixtures.Document(CvFixtures.Paragraph("BODY")), parts);

        string text = DocxText.Extract(data);

        Assert.Equal("header-1\nheader-2\nheader-10\nBODY\nfooter-1\nfooter-2\nfooter-10", text);
    }

    [Fact]
    public void ShouldReadNotesAfterBody_WhenWordHasNotes() {
        byte[] data = CvFixtures.Docx(
            CvFixtures.Document(CvFixtures.Paragraph("BODY")),
            ("word/footer1.xml", CvFixtures.WordPart("ftr", CvFixtures.Paragraph("FOOTER"))),
            ("word/endnotes.xml", CvFixtures.WordPart("endnotes", CvFixtures.Paragraph("ENDNOTE"))),
            ("word/footnotes.xml", CvFixtures.WordPart("footnotes", CvFixtures.Paragraph("FOOTNOTE"))));

        string text = DocxText.Extract(data);

        Assert.Equal("BODY\nFOOTNOTE\nENDNOTE\nFOOTER", text);
    }

    [Fact]
    public void ShouldWriteHyphens_WhenWordUsesHyphenElements() {
        byte[] data = CvFixtures.Docx(CvFixtures.Document(
            "<w:p><w:r><w:t>e</w:t><w:noBreakHyphen/><w:t>post</w:t></w:r></w:p>"
            + "<w:p><w:r><w:t>data</w:t><w:softHyphen/><w:t>base</w:t></w:r></w:p>"));

        string text = DocxText.Extract(data);

        Assert.Equal("e-post\ndatabase", text);
    }

    [Theory]
    [InlineData(1, CvText.MaxChars, null)]
    [InlineData(2, halfTextCap, CvUnreadableException.TooLarge)]
    public void ShouldCapText_WhenWordPassesTextLimit(int paragraphs, int length, string? expected) {
        byte[] data = CvFixtures.Docx(CvFixtures.Document(
            string.Concat(Enumerable.Repeat(CvFixtures.Paragraph(new string('x', length)), paragraphs))));

        string? reason = CvFixtures.Reason(() => DocxText.Extract(data));

        Assert.Equal(expected, reason);
    }

    [Theory]
    [InlineData(CvText.MaxChars - 1, null)]
    [InlineData(CvText.MaxChars, CvUnreadableException.TooLarge)]
    public void ShouldCapPendingLineBreaks_WhenWordHasBlankParagraphs(int blankParagraphs, string? expected) {
        byte[] data = CvFixtures.Docx(CvFixtures.Document(
            CvFixtures.Paragraph("x") + string.Concat(Enumerable.Repeat("<w:p/>", blankParagraphs))));

        string? reason = CvFixtures.Reason(() => DocxText.Extract(data));

        Assert.Equal(expected, reason);
    }

    [Theory]
    [InlineData(DocxText.MaxUnpackedBytes, null)]
    [InlineData(DocxText.MaxUnpackedBytes + 1, CvUnreadableException.TooLarge)]
    public void ShouldThrowTooLarge_WhenUnpackedWordPassesBudget(int size, string? expected) {
        byte[] data = CvFixtures.Docx(CvFixtures.PaddedPart(CvFixtures.Document, size));

        string? reason = CvFixtures.Reason(() => DocxText.Extract(data));

        Assert.Equal(expected, reason);
    }

    [Fact]
    public void ShouldThrowTooLarge_WhenPartsTogetherPassBudget() {
        byte[] data = CvFixtures.Docx(CvFixtures.PaddedPart(CvFixtures.Document, overHalfUnpackBudget),
            ("word/header1.xml", CvFixtures.PaddedPart(body => CvFixtures.WordPart("hdr", body), overHalfUnpackBudget)));

        string? reason = CvFixtures.Reason(() => DocxText.Extract(data));

        Assert.Equal(CvUnreadableException.TooLarge, reason);
    }

    private static (string, byte[]) Part(string kind, string root, int number) =>
        ($"word/{kind}{number}.xml", CvFixtures.WordPart(root, CvFixtures.Paragraph($"{kind}-{number}")));
}
