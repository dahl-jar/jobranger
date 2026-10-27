namespace Jobranger.Cli.Features.Matching;

internal sealed record Check(string Text, string State) {
    public static readonly IReadOnlySet<string> States =
        new HashSet<string>(StringComparer.Ordinal) { "met", "partly", "missing" };
}
