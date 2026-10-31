namespace Jobranger.Cli.Features.Learning;

internal static class Learning {
    private const int descriptionLimit = 600;
    private const string separator = ". ";

    public static string EmbedText(string title, string? description) {
        if (string.IsNullOrWhiteSpace(description)) {
            return title;
        }
        int length = Math.Min(description.Length, descriptionLimit);
        if (char.IsHighSurrogate(description[length - 1])) {
            length--;
        }
        return title + separator + description[..length];
    }
}
