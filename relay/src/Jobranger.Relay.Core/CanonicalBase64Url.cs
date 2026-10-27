using System.Buffers;
using System.Buffers.Text;

namespace Jobranger.Relay.Core;

public static class CanonicalBase64Url
{
    private static readonly SearchValues<char> alphabet =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_");

    public static bool TryDecode(string? text, int byteLength, out byte[] bytes)
    {
        bytes = [];
        return text is not null
            && text.Length == Base64Url.GetEncodedLength(byteLength)
            && TryDecode(text, out bytes);
    }

    public static bool TryDecode(ReadOnlySpan<char> text, out byte[] bytes)
    {
        bytes = [];
        if (!IsCanonical(text))
        {
            return false;
        }

        bytes = Base64Url.DecodeFromChars(text);
        return true;
    }

    public static bool IsCanonical(ReadOnlySpan<char> text) => !text.ContainsAnyExcept(alphabet) && Base64Url.IsValid(text);
}
