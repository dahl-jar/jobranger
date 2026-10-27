using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Budget;

[UnsupportedOSPlatform("windows")]
public sealed class UsageStoreTests : IDisposable {
    private static readonly DateOnly octoberFirst = new(2026, 10, 1);
    private static readonly DateOnly novemberFirst = new(2026, 11, 1);
    private static readonly DateOnly day = new(2026, 10, 9);

    private readonly TestDatabase db = new();
    private readonly UsageStore usage;

    public UsageStoreTests() {
        usage = new(db.Connection);
    }

    public static TheoryData<object, double> StoredCosts() =>
        [("abc", 0.0), (1e308, (double)AppConfigValidator.MaxAmountUsd)];

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldAddUp_WhenUsageIsAddedTwiceOnSameDay() {
        UsageCounts call = new(Calls: 1, InputTokens: 10, OutputTokens: 5, CostUsd: 0.5, AdsChecked: 1);

        usage.Add(day, call);
        usage.Add(day, call with { AdsRead = 7 });

        Assert.Equal([new UsageDay("2026-10-09", 2, 20, 10, 1.0, 7, 2)], usage.Days(octoberFirst));
    }

    [Fact]
    public void ShouldStoreUsage_WhenCostIsZero() {
        usage.Add(day, new UsageCounts(AdsRead: 3));

        IReadOnlyList<UsageDay> days = usage.Days(octoberFirst);

        Assert.Equal([new UsageDay("2026-10-09", 0, 0, 0, 0.0, 3, 0)], days);
    }

    [Fact]
    public void ShouldSumSpend_WhenDayIsInsideRange() {
        foreach ((DateOnly spentOn, double cost) in new[] {
            (new DateOnly(2026, 9, 30), 4.0), (octoberFirst, 0.5), (day, 1.0), (novemberFirst, 2.0) }) {
            usage.Add(spentOn, new UsageCounts(Calls: 1, CostUsd: cost));
        }

        Assert.Equal(1.5, usage.SpendBetween(octoberFirst, novemberFirst));
    }

    [Fact]
    public void ShouldReportZeroSpend_WhenNoUsageExists() {
        Assert.Equal(0.0, usage.SpendBetween(octoberFirst, novemberFirst));
    }

    [Fact]
    public void ShouldReadCostAsZero_WhenStoredCostIsNegative() {
        db.Execute("INSERT INTO usage_days (day, cost_usd) VALUES ('2026-10-03', -1.0)");

        Assert.Equal(0.0, usage.SpendBetween(octoberFirst, novemberFirst));
    }

    [Theory]
    [MemberData(nameof(StoredCosts))]
    public void ShouldClampStoredCost_WhenReadingDays(object stored, double expected) {
        db.Execute("INSERT INTO usage_days (day, cost_usd) VALUES ('2026-10-03', $0)", stored);

        Assert.Equal(expected, usage.Days(octoberFirst).Single().CostUsd);
    }

    [Theory]
    [InlineData("x")]
    [InlineData(-5L)]
    [InlineData(new byte[] { 0x00, 0xff })]
    public void ShouldReadCountsAsZero_WhenStoredCountsAreCorrupt(object count) {
        db.Execute(
            "INSERT INTO usage_days (day, calls, input_tokens, output_tokens, cost_usd, ads_read, ads_checked) "
            + "VALUES ('2026-10-03', $0, $0, $0, 0.5, $0, $0)", count);

        Assert.Equal([new UsageDay("2026-10-03", 0, 0, 0, 0.5, 0, 0)], usage.Days(octoberFirst));
    }

    [Theory]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    [InlineData(-0.01)]
    public void ShouldRejectCost_WhenNotFiniteOrNegative(double cost) {
        ArgumentOutOfRangeException refused =
            Assert.Throws<ArgumentOutOfRangeException>(() => usage.Add(day, new UsageCounts(Calls: 1, CostUsd: cost)));

        Assert.Contains("cost_usd", refused.Message, StringComparison.Ordinal);
    }
}
