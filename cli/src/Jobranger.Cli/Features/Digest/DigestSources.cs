using System.Collections.Frozen;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class DigestSources {
    private const char kindSeparator = ':';

    private static readonly FrozenDictionary<string, string> brandLabels = new Dictionary<string, string> {
        ["finn"] = "FINN",
        ["nav"] = "NAV",
        ["linkedin"] = "LinkedIn",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool IsDirect(string source) =>
        EmployerFeed.AllKinds.Any(kind => source.StartsWith(kind + kindSeparator, StringComparison.Ordinal));

    public static string Label(string source, IMessages messages) =>
        IsDirect(source) ? messages.DigestCareerPage : brandLabels.GetValueOrDefault(source, source);

    public static string Phrase(string source, IMessages messages) {
        if (IsDirect(source)) {
            string key = source[(source.IndexOf(kindSeparator, StringComparison.Ordinal) + 1)..];
            return messages.DigestCareerPageOf(CardText.Cut(key, CardText.FieldLimit));
        }
        return CardText.Cut(brandLabels.GetValueOrDefault(source, source), CardText.FieldLimit);
    }
}
