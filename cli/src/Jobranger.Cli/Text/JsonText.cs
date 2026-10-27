using System.Text.Json;

namespace Jobranger.Cli.Text;

internal static class JsonText {
    public static string? String(JsonElement element) {
        if (element.ValueKind != JsonValueKind.String) {
            return null;
        }
        try {
            return element.GetString();
        } catch (InvalidOperationException) {
            return null;
        }
    }
}
