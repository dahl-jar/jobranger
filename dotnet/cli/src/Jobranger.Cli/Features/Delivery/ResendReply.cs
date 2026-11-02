using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Delivery;

internal static class ResendReply {
    private const string messageField = "message";
    private const int reasonLimit = 200;

    public static string Message(byte[] body) {
        if (Read(body) is not string text) {
            return string.Empty;
        }
        StringBuilder kept = new(text.Length);
        foreach (int code in CodePoints.Of(text).Where(code => PythonUnicode.IsPrintable(code) || PythonUnicode.IsSpace(code))) {
            CodePoints.Append(kept, code);
        }
        string collapsed = PythonUnicode.CollapseSpace(kept.ToString());
        return collapsed[..CodePoints.IndexAfter(collapsed, reasonLimit)];
    }

    private static string? Read(byte[] body) {
        try {
            return JsonNode.Parse(body)?[messageField]?.GetValue<string>();
        } catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException) {
            return null;
        }
    }
}
