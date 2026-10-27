namespace Jobranger.Contracts.Runs;

public sealed record ModelStep(string State, long Checked, long Total, string? ResultsBy);
