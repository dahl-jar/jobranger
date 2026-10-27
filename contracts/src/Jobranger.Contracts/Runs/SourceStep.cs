namespace Jobranger.Contracts.Runs;

public sealed record SourceStep(string Source, string State, long? FoundNew);
