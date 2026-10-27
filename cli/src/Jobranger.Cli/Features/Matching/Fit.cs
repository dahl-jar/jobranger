namespace Jobranger.Cli.Features.Matching;

internal static class Fit {
    public const string Strong = "strong";
    public const string Stretch = "stretch";
    public const string Unsuitable = "unsuitable";
    public const string Unknown = "unknown";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.Ordinal) { Strong, Stretch, Unsuitable, Unknown };

    public static readonly IReadOnlySet<string> Reportable =
        new HashSet<string>(StringComparer.Ordinal) { Strong, Stretch };
}
