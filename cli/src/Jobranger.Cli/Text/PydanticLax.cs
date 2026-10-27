using System.Collections.Frozen;

namespace Jobranger.Cli.Text;

internal static class PydanticLax {
    private const char underscore = '_';
    private const string doubleUnderscore = "__";

    private static readonly FrozenSet<string> trueWords =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "1", "t", "y", "on", "yes", "true");
    private static readonly FrozenSet<string> falseWords =
        FrozenSet.Create(StringComparer.OrdinalIgnoreCase, "0", "f", "n", "no", "off", "false");

    public static bool? Bool(string text) {
        if (trueWords.Contains(text)) {
            return true;
        }
        return falseWords.Contains(text) ? false : null;
    }

    public static double? Float(string text) {
        if (RustFloat.Parse(text.Trim()) is double number) {
            return number;
        }
        if (!text.Contains(underscore, StringComparison.Ordinal)
            || text[0] == underscore
            || text[^1] == underscore
            || text.Contains(doubleUnderscore, StringComparison.Ordinal)) {
            return null;
        }
        return RustFloat.Parse(text.Replace(underscore.ToString(), string.Empty, StringComparison.Ordinal));
    }
}
