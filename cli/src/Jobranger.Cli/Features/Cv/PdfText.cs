using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Jobranger.Cli.Features.Cv;

internal static class PdfText {
    public const int MaxPages = 50;

    private const char pageSeparator = '\n';

    private static readonly ParsingOptions options = new() { SkipMissingFonts = true };

    public static string Extract(byte[] data) {
        using PdfDocument document = PdfDocument.Open(data, options);
        if (document.NumberOfPages > MaxPages) {
            throw new CvUnreadableException(CvUnreadableException.TooLarge);
        }
        CvTextBuffer text = new();
        for (int number = 1; number <= document.NumberOfPages; number++) {
            if (number > 1) {
                text.Separate(pageSeparator);
            }
            text.Append(ContentOrderTextExtractor.GetText(document.GetPage(number)));
        }
        return text.Value();
    }
}
