namespace Jobranger.Contracts.Settings.Config;

public sealed record Schedule(IReadOnlyList<string> Times, int MinIntervalHours);
