using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Matching;

internal static class MatchText {
    public static string Head(string text, int limit) => text[..CodePoints.IndexAfter(text, limit)];
}
