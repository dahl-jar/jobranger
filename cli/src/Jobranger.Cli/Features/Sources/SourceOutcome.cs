namespace Jobranger.Cli.Features.Sources;

internal sealed record SourceOutcome(string Source, SourceBatch? Batch, string? Error, bool Cancelled);
