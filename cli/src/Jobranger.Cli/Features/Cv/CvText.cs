using System.Buffers;
using System.Globalization;
using System.Text;

namespace Jobranger.Cli.Features.Cv;

internal static class CvText {
    public const int MaxBytes = 5_242_880;
    public const int MaxChars = 100_000;
    public const int MinChars = 200;

    private const int cp1252 = 1252;
    private const int asciiMax = 0x7F;
    private const char replacement = '�';
    private const string carriageReturnLineFeed = "\r\n";
    private const string carriageReturn = "\r";
    private const string lineFeed = "\n";

    private static readonly byte[] utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] utf16LittleEndianBom = [0xFF, 0xFE];
    private static readonly byte[] utf16BigEndianBom = [0xFE, 0xFF];
    private static readonly byte[] pdfMagic = "%PDF-"u8.ToArray();
    private static readonly SearchValues<byte> asciiWhiteSpace = SearchValues.Create(" \t\n\r\v\f"u8);
    private static readonly UTF8Encoding strictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding lenientUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    private static readonly Encoding windows1252 = CodePagesEncodingProvider.Instance.GetEncoding(cp1252)!;
    private static readonly SearchValues<char> keptChars = SearchValues.Create("\t\n\u200C\u200D");

    public static string FromTextFile(byte[] data) => Checked(Decode(data));

    public static string Checked(string raw) {
        string text = Clean(raw);
        int length = 0;
        int visible = 0;
        foreach (Rune rune in text.EnumerateRunes()) {
            length++;
            if (!Rune.IsWhiteSpace(rune)) {
                visible++;
            }
        }
        if (length > MaxChars) {
            throw new CvUnreadableException(CvUnreadableException.TooLarge);
        }
        if (visible < MinChars) {
            throw new CvUnreadableException(CvUnreadableException.NoText);
        }
        return text;
    }

    private static string Decode(byte[] data) {
        ReadOnlySpan<byte> bytes = data;
        if (bytes.StartsWith(utf16LittleEndianBom) || bytes.StartsWith(utf16BigEndianBom)) {
            return DecodeUtf16(bytes);
        }
        int start = bytes.IndexOfAnyExcept(asciiWhiteSpace);
        if (bytes.Contains((byte)0) || (start >= 0 && bytes[start..].StartsWith(pdfMagic))) {
            throw new CvUnreadableException(CvUnreadableException.Unreadable);
        }
        ReadOnlySpan<byte> body = bytes.StartsWith(utf8Bom) ? bytes[utf8Bom.Length..] : bytes;
        try {
            return strictUtf8.GetString(body);
        } catch (DecoderFallbackException) {
            string lenient = lenientUtf8.GetString(body);
            int invalid = lenient.Count(character => character == replacement);
            int multibyte = lenient.EnumerateRunes().Count(rune => rune.Value > asciiMax) - invalid;
            string decoded = multibyte > invalid ? lenient : windows1252.GetString(body);
            return decoded.Replace(replacement.ToString(), string.Empty, StringComparison.Ordinal);
        }
    }

    private static string DecodeUtf16(ReadOnlySpan<byte> bytes) {
        bool bigEndian = bytes.StartsWith(utf16BigEndianBom);
        UnicodeEncoding encoding = new(bigEndian, byteOrderMark: false, throwOnInvalidBytes: true);
        try {
            return encoding.GetString(bytes[utf16LittleEndianBom.Length..]);
        } catch (DecoderFallbackException error) {
            throw new CvUnreadableException(CvUnreadableException.Unreadable, error);
        }
    }

    private static string Clean(string text) {
        ReadOnlySpan<char> rest = text
            .Replace(carriageReturnLineFeed, lineFeed, StringComparison.Ordinal)
            .Replace(carriageReturn, lineFeed, StringComparison.Ordinal);
        StringBuilder cleaned = new(rest.Length);
        while (!rest.IsEmpty) {
            OperationStatus status = Rune.DecodeFromUtf16(rest, out Rune rune, out int consumed);
            if (status == OperationStatus.Done && IsSafe(rune)) {
                cleaned.Append(rest[..consumed]);
            }
            rest = rest[consumed..];
        }
        return cleaned.ToString();
    }

    private static bool IsSafe(Rune rune) {
        if (rune.IsBmp && keptChars.Contains((char)rune.Value)) {
            return true;
        }
        return Rune.GetUnicodeCategory(rune) is not (UnicodeCategory.Control or UnicodeCategory.Format
            or UnicodeCategory.Surrogate);
    }
}
