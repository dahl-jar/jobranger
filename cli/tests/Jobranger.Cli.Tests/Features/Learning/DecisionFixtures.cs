using System.Globalization;
using Jobranger.Cli.Features.Learning;

namespace Jobranger.Cli.Tests.Features.Learning;

internal static class DecisionFixtures {
    public const string CurrentModel = "model-one";
    public const string OldModel = "model-old";

    public static readonly DateTimeOffset DecidedAt = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);

    public static readonly NewDecision Primary = new(
        "finn:1",
        "org one as|backend developer",
        DecisionAction.Applied,
        DecidedAt,
        "Backend Developer",
        "Org One AS",
        "Build Go services.",
        ["Go", "Bachelor's degree"],
        "Backend Developer. Build Go services in Bergen.");

    public static NewDecision Numbered(int index) {
        string number = index.ToString(CultureInfo.InvariantCulture);
        return Primary with { AdId = "finn:" + number, DedupKey = "org one as|developer " + number };
    }
}
