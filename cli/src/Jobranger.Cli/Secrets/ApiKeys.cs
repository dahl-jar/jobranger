namespace Jobranger.Cli.Secrets;

internal static class ApiKeys {
    private const int minKeyChars = KeyGuard.FragmentChars;
    private const char firstKeyChar = '!';
    private const char lastKeyChar = '~';

    public static string RequireSaved(string label, string? key, Func<string, Exception> refuse) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw refuse($"no API key saved for {label}");
        }
        try {
            return Require(label, key);
        } catch (ArgumentException error) {
            throw refuse(error.Message);
        }
    }

    public static string Require(string label, string? key) {
        string stripped = key?.Trim() ?? string.Empty;
        if (stripped.Length == 0) {
            throw new ArgumentException($"{label} needs an API key");
        }
        if (!stripped.All(character => character is >= firstKeyChar and <= lastKeyChar)) {
            throw new ArgumentException($"{label} API key must be printable ASCII");
        }
        return stripped.Length < minKeyChars
            ? throw new ArgumentException($"{label} API key is too short")
            : stripped;
    }
}
