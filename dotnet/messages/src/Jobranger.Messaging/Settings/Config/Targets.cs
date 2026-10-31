namespace Jobranger.Messaging.Settings.Config;

public sealed record Targets(
    IReadOnlyList<string> Subfields,
    IReadOnlyList<string> Levels,
    IReadOnlyList<string> Years,
    bool HideMasters,
    IReadOnlyList<string> Places,
    bool Relocate);
