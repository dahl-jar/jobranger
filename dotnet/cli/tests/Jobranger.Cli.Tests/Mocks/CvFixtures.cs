using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Features.Cv;
using Jobranger.Cli.Files;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Tests.Mocks;

internal static class CvFixtures {
    public const string PdfTextLine = "BT /F1 12 Tf 50 700 Td (Backend developer at Employer One AS) Tj ET\n";
    public const int ReplyLimit = 1_600_000;

    private const string documentMember = "word/document.xml";
    private const string pdfFont = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>";
    private const string transitionalWord = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string strictWord = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    private const string wordNamespaces =
        "xmlns:w=\"" + transitionalWord + "\" "
        + "xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" "
        + "xmlns:v=\"urn:schemas-microsoft-com:vml\" "
        + "xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\"";
    private const string dotnetName = "dotnet";
    private const string workerAssembly = "jobranger.dll";
    private const int runtimeDepth = 3;

    public static string ValidText { get; } = string.Concat(Enumerable.Repeat("Backend developer at Employer One AS. ", 10));

    public static string ValidReply { get; } = $"{{\"text\":\"{ValidText}\"}}";

    public static string LongLine { get; } =
        string.Concat(Enumerable.Repeat("Backend developer at Employer One AS building data pipelines. ", 5));

    public static string CvPdf { get; } = TestFile("cv_user.pdf");

    public static string CvMarkdown { get; } = TestFile("cv_user.md");

    public static string Muxer { get; } = Path.GetFullPath(Path.Combine(
        RuntimeEnvironment.GetRuntimeDirectory(), string.Concat(Enumerable.Repeat("../", runtimeDepth)), dotnetName));

    public static string WorkerDll { get; } = Path.Combine(AppContext.BaseDirectory, workerAssembly);

    public static byte[] PdfUpload { get; } = "%PDF-"u8.ToArray();

    [UnsupportedOSPlatform("windows")]
    public static CvWorkerRunner Runner(
        IChildProcessRunner runner, string program, IReadOnlyList<string> arguments, string tempRoot, TimeSpan deadline,
        IDictionary? environment = null, uint? userId = null) =>
        new(runner, program, arguments, environment ?? new Hashtable(), tempRoot, userId ?? UnixFiles.EffectiveUserId(), deadline,
            TimeProvider.System);

    [UnsupportedOSPlatform("windows")]
    public static string WorkdirParent(string tempRoot) =>
        Path.Combine(tempRoot, CvWorkerRunner.WorkdirPrefix + UnixFiles.EffectiveUserId().ToString(CultureInfo.InvariantCulture));

    public static string? Reason(Func<string> read) {
        try {
            read();
        } catch (CvUnreadableException error) {
            return error.Reason;
        }
        return null;
    }

    public static async Task<string?> ReasonAsync(Func<Task<string>> read) {
        try {
            await read();
        } catch (CvUnreadableException error) {
            return error.Reason;
        }
        return null;
    }

    public static byte[] PlainStream(string content) =>
        Encoding.Latin1.GetBytes($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");

    public static byte[] FlateStream(byte[] content) {
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Fastest, leaveOpen: true)) {
            zlib.Write(content);
        }
        byte[] data = compressed.ToArray();
        return [
            .. Encoding.Latin1.GetBytes($"<< /Length {data.Length} /Filter /FlateDecode >>\nstream\n"),
            .. data,
            .. "\nendstream"u8,
        ];
    }

    public static byte[] PdfPage(string text) =>
        FlateStream(Encoding.Latin1.GetBytes($"BT /F1 12 Tf 72 720 Td ({text}) Tj ET"));

    public static byte[] Pdf(IReadOnlyList<byte[]> pageStreams) {
        int count = pageStreams.Count;
        int firstPage = 3 + count + 1;
        string kids = string.Join(' ', Enumerable.Range(0, count).Select(index => $"{firstPage + index} 0 R"));
        List<byte[]> objects = [
            Encoding.Latin1.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Encoding.Latin1.GetBytes($"<< /Type /Pages /Kids [{kids}] /Count {count} >>"),
            Encoding.Latin1.GetBytes(pdfFont),
            .. pageStreams,
        ];
        objects.AddRange(Enumerable.Range(0, count).Select(index => Encoding.Latin1.GetBytes(
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] "
            + $"/Resources << /Font << /F1 3 0 R >> >> /Contents {4 + index} 0 R >>")));
        using MemoryStream output = new();
        output.Write("%PDF-1.7\n"u8);
        List<long> offsets = [];
        for (int number = 1; number <= objects.Count; number++) {
            offsets.Add(output.Position);
            output.Write(Encoding.Latin1.GetBytes($"{number} 0 obj\n"));
            output.Write(objects[number - 1]);
            output.Write("\nendobj\n"u8);
        }
        long xref = output.Position;
        StringBuilder table = new($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (long offset in offsets) {
            table.Append(offset.ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        table.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        output.Write(Encoding.Latin1.GetBytes(table.ToString()));
        return output.ToArray();
    }

    public static string Paragraph(string text) => $"<w:p><w:r><w:t>{text}</w:t></w:r></w:p>";

    public static byte[] WordPart(string root, string body) => Encoding.UTF8.GetBytes(
        $"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><w:{root} {wordNamespaces}>{body}</w:{root}>");

    public static byte[] Document(string body) => WordPart("document", $"<w:body>{body}</w:body>");

    public static byte[] StrictDocument(string body) =>
        Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Document(body)).Replace(transitionalWord, strictWord, StringComparison.Ordinal));

    public static byte[] PaddedPart(Func<string, byte[]> part, int size) {
        string body = Paragraph("x");
        int padding = size - part(body).Length;
        return part(new string(' ', padding) + body);
    }

    public static byte[] Docx(byte[]? mainPart, params (string Name, byte[] Content)[] parts) {
        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true)) {
            if (mainPart is not null) {
                Add(archive, documentMember, mainPart);
            }
            foreach ((string name, byte[] content) in parts) {
                Add(archive, name, content);
            }
        }
        return output.ToArray();
    }

    private static void Add(ZipArchive archive, string name, byte[] content) {
        using Stream entry = archive.CreateEntry(name, CompressionLevel.Fastest).Open();
        entry.Write(content);
    }

    private static string TestFile(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", "Cv", name);
}
