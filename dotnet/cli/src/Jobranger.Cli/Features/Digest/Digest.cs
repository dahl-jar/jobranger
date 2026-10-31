using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Digest;

internal static class Digest {
    public static DigestData Build(DigestInput input, DateOnly today) => DigestBuilder.Build(input, today);

    public static RenderedDigest Render(DigestData data, string? siteUrl, IMessages messages) =>
        DigestRenderer.Render(data, siteUrl, messages);

    public static string HiddenLine(IReadOnlyList<HiddenCount> hidden, double? maxYears, IMessages messages) =>
        TerminalLines.HiddenLine(hidden, maxYears, messages);

    public static string SourcePhrase(string source, IMessages messages) => DigestSources.Phrase(source, messages);
}
