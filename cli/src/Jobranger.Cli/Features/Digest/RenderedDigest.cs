namespace Jobranger.Cli.Features.Digest;

internal sealed record RenderedDigest(string Subject, string Html, string Text, IReadOnlyList<string> AdIds);
