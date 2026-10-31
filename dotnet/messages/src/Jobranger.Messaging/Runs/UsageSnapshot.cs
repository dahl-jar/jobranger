using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Runs;

public sealed record UsageSnapshot(
    string Month,
    long MonthTokens,
    long TodayTokens,
    double MonthUsd,
    double TodayUsd,
    string ResetsOn,
    IReadOnlyList<UsageDay> Days) : PayloadBase(Tag) {
    public const string Tag = "usage.snapshot";
}
