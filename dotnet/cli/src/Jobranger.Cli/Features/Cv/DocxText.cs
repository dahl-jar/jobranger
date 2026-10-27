using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;

namespace Jobranger.Cli.Features.Cv;

internal static partial class DocxText {
    public const int MaxUnpackedBytes = 52_428_800;

    private const string wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string strictWordNamespace = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    private const string compatibilityNamespace = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private const string fallbackName = "Fallback";
    private const string textName = "t";
    private const string noBreakHyphenName = "noBreakHyphen";
    private const string noBreakHyphen = "-";
    private const string documentPart = "word/document.xml";

    private static readonly string[] noteParts = ["word/footnotes.xml", "word/endnotes.xml"];

    private static readonly Dictionary<string, char> tagSeparators = new(StringComparer.Ordinal) {
        ["p"] = '\n',
        ["tab"] = '\t',
        ["br"] = '\n',
        ["cr"] = '\n',
    };

    private static readonly XmlReaderSettings settings = new() {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = true,
    };

    public static string Extract(byte[] data) {
        CvTextBuffer text = new();
        UnpackBudget budget = new(MaxUnpackedBytes);
        using ZipArchive archive = new(new MemoryStream(data), ZipArchiveMode.Read);
        foreach (string name in Parts([.. archive.Entries.Select(entry => entry.FullName)])) {
            ZipArchiveEntry entry = archive.GetEntry(name)
                ?? throw new InvalidDataException($"docx has no part name={name}");
            using XmlReader reader = XmlReader.Create(new BudgetStream(entry.Open(), budget), settings);
            AppendPartText(reader, text);
        }
        return text.Value();
    }

    private static List<string> Parts(IReadOnlyList<string> names) {
        IEnumerable<string> notes = noteParts.Where(names.Contains);
        return [.. Ordered(names, HeaderPart()), documentPart, .. notes, .. Ordered(names, FooterPart())];
    }

    private static IEnumerable<string> Ordered(IEnumerable<string> names, Regex pattern) =>
        names.Where(name => pattern.IsMatch(name)).OrderBy(name => name.Length).ThenBy(name => name, StringComparer.Ordinal);

    private static void AppendPartText(XmlReader reader, CvTextBuffer text) {
        int fallbackDepth = 0;
        int textDepth = -1;
        while (reader.Read()) {
            switch (reader.NodeType) {
                case XmlNodeType.Element when IsFallback(reader):
                    fallbackDepth += reader.IsEmptyElement ? 0 : 1;
                    break;
                case XmlNodeType.Element when fallbackDepth == 0 && IsWordText(reader) && !reader.IsEmptyElement:
                    textDepth = reader.Depth;
                    break;
                case XmlNodeType.Element when fallbackDepth == 0 && reader.IsEmptyElement:
                    AppendEnd(reader, text);
                    break;
                case XmlNodeType.EndElement when IsFallback(reader):
                    fallbackDepth--;
                    break;
                case XmlNodeType.EndElement when fallbackDepth == 0:
                    textDepth = reader.Depth == textDepth ? -1 : textDepth;
                    AppendEnd(reader, text);
                    break;
                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace
                    when textDepth >= 0 && reader.Depth == textDepth + 1:
                    text.Append(reader.Value);
                    break;
            }
        }
    }

    private static void AppendEnd(XmlReader reader, CvTextBuffer text) {
        if (!IsWordNamespace(reader)) {
            return;
        }
        if (reader.LocalName == noBreakHyphenName) {
            text.Append(noBreakHyphen);
        } else if (tagSeparators.TryGetValue(reader.LocalName, out char separator)) {
            text.Separate(separator);
        }
    }

    private static bool IsFallback(XmlReader reader) =>
        reader.NamespaceURI == compatibilityNamespace && reader.LocalName == fallbackName;

    private static bool IsWordText(XmlReader reader) => IsWordNamespace(reader) && reader.LocalName == textName;

    private static bool IsWordNamespace(XmlReader reader) => reader.NamespaceURI is wordNamespace or strictWordNamespace;

    [GeneratedRegex("^word/header[0-9]*\\.xml$")]
    private static partial Regex HeaderPart();

    [GeneratedRegex("^word/footer[0-9]*\\.xml$")]
    private static partial Regex FooterPart();

    private sealed class UnpackBudget(long limit) {
        private long remaining = limit;

        public void Spend(int bytes) {
            remaining -= bytes;
            if (remaining < 0) {
                throw new CvUnreadableException(CvUnreadableException.TooLarge);
            }
        }
    }

    private sealed class BudgetStream(Stream inner, UnpackBudget budget) : Stream {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer) {
            int read = inner.Read(buffer);
            budget.Spend(read);
            return read;
        }

        public override void Flush() {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) {
            if (disposing) {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
