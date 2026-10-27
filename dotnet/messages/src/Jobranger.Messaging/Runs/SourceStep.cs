namespace Jobranger.Messaging.Runs;

public sealed record SourceStep(string Source, string State, long? FoundNew);
