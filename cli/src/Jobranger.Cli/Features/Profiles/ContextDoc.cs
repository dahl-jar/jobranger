using System.Globalization;
using System.Text.RegularExpressions;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Profiles;

internal sealed partial record ContextDoc(string Id, string Name, string Text, string AddedAt) {
    public const int NameLimit = 120;

    private const string idPrefix = "doc_";
    private const int idLimit = 40;
    private const int extensionLimit = 16;
    private const int firstClashSuffix = 2;
    private const char slugSeparator = '_';
    private const char pathSeparator = '/';
    private const char extensionMark = '.';
    private const string fallbackSlug = "file";
    private const string dateFormat = "yyyy-MM-dd";

    public static ContextDoc Create(string name, string text, DateOnly today, IReadOnlySet<string> taken) =>
        new(NewId(name, taken), DisplayName(name), text, today.ToString(dateFormat, CultureInfo.InvariantCulture));

    public static bool IsId(string id) => IdPattern().IsMatch(id);

    public static bool HasIdPrefix(string id) => id.StartsWith(idPrefix, StringComparison.Ordinal);

    public static bool IsDate(string text) =>
        DateOnly.TryParseExact(text, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static string NewId(string name, IReadOnlySet<string> taken) {
        string slug = NonSlug().Replace(PythonCase.Lower(name), slugSeparator.ToString()).Trim(slugSeparator);
        string full = idPrefix + (slug.Length > 0 ? slug : fallbackSlug);
        string id = full.Length > idLimit ? full[..idLimit] : full;
        string candidate = id;
        for (int clash = firstClashSuffix; taken.Contains(candidate); clash++) {
            string suffix = slugSeparator + clash.ToString(CultureInfo.InvariantCulture);
            candidate = id[..Math.Min(id.Length, idLimit - suffix.Length)] + suffix;
        }
        return candidate;
    }

    private static string DisplayName(string name) {
        if (CodePoints.Count(name) <= NameLimit) {
            return name;
        }
        string extension = Extension(name);
        extension = extension[..CodePoints.IndexAfter(extension, extensionLimit)];
        return name[..CodePoints.IndexAfter(name, NameLimit - CodePoints.Count(extension))] + extension;
    }

    private static string Extension(string name) {
        string trimmed = name.TrimEnd(pathSeparator);
        string last = trimmed[(trimmed.LastIndexOf(pathSeparator) + 1)..];
        int mark = last.LastIndexOf(extensionMark);
        return mark > 0 && mark < last.Length - 1 ? last[mark..] : string.Empty;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();

    [GeneratedRegex(@"^doc_[a-z0-9_]{1,36}\z")]
    private static partial Regex IdPattern();
}
