using System.Buffers;
using System.Globalization;
using System.Text.Encodings.Web;

namespace Jobranger.Messaging.Payloads;

internal sealed class CompactJsonEncoder : JavaScriptEncoder {
    private const int firstPrintable = 0x20;
    private const int escapeWidth = 6;
    private const char quote = '"';
    private const char backslash = '\\';
    private const char firstSurrogate = '\uD800';
    private const char lastSurrogate = '\uDFFF';

    private static readonly SearchValues<char> escapedChars = SearchValues.Create(EscapedText());
    private static readonly SearchValues<byte> escapedBytes = SearchValues.Create([.. EscapedText().Select(code => (byte)code)]);

    public static CompactJsonEncoder Instance { get; } = new();

    public override int MaxOutputCharactersPerInputCharacter => escapeWidth;

    public override bool WillEncode(int unicodeScalar) => unicodeScalar is < firstPrintable or quote or backslash;

    public override int FindFirstCharacterToEncodeUtf8(ReadOnlySpan<byte> utf8Text) => utf8Text.IndexOfAny(escapedBytes);

    public override unsafe int FindFirstCharacterToEncode(char* text, int textLength) {
        ReadOnlySpan<char> span = new(text, textLength);
        if (HasLoneSurrogate(span)) {
            throw new ArgumentException("text holds a lone surrogate", nameof(text));
        }
        return span.IndexOfAny(escapedChars);
    }

    public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten) {
        string escaped = Escape(unicodeScalar);
        Span<char> destination = new(buffer, bufferLength);
        if (!escaped.AsSpan().TryCopyTo(destination)) {
            numberOfCharactersWritten = 0;
            return false;
        }
        numberOfCharactersWritten = escaped.Length;
        return true;
    }

    private static string Escape(int scalar) => scalar switch {
        quote => "\\\"",
        backslash => "\\\\",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        '\b' => "\\b",
        '\f' => "\\f",
        _ => string.Create(CultureInfo.InvariantCulture, $"\\u{scalar:x4}"),
    };

    private static bool HasLoneSurrogate(ReadOnlySpan<char> text) {
        int start = text.IndexOfAnyInRange(firstSurrogate, lastSurrogate);
        if (start < 0) {
            return false;
        }
        for (int index = start; index < text.Length; index++) {
            if (char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])) {
                index++;
            } else if (char.IsSurrogate(text[index])) {
                return true;
            }
        }
        return false;
    }

    private static string EscapedText() =>
        string.Concat(Enumerable.Range(0, firstPrintable).Select(code => (char)code)) + quote + backslash;
}
