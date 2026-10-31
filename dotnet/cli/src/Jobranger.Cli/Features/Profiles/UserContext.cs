using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Profiles;

internal sealed record UserContext {
    public string About { get; init; } = string.Empty;

    public IReadOnlyList<ContextDoc> Docs { get; init; } = [];

    public int TextLength => CodePoints.Count(About) + Docs.Sum(doc => CodePoints.Count(doc.Text));
}
