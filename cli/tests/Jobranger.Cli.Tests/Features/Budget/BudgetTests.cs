using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Tests.Mocks;
using static Jobranger.Cli.Tests.Mocks.BudgetGolden;
using BudgetEntry = Jobranger.Cli.Features.Budget.Budget;

namespace Jobranger.Cli.Tests.Features.Budget;

[UnsupportedOSPlatform("windows")]
public sealed class BudgetTests : IDisposable {
    private const decimal inFlightUsd = 0.01m;
    private const decimal oneMicroUsd = 0.000001m;
    private const int windowAds = 12;
    private const long oneMtok = 1_000_000;
    private const long promptCharsPerMtok = 2_000_000;

    private static readonly DateOnly today = new(2026, 10, 15);
    private static readonly ModelPrices free = new(0m, 0m, 0m, 0m, 0m);

    private readonly TestDatabase db = new();
    private readonly UsageStore usage;
    private readonly BudgetEntry budget;

    public BudgetTests() {
        usage = new(db.Connection);
        budget = new(db.Connection);
    }

    public static TheoryData<decimal> OutOfRangeAmounts() => [-1m, AppConfigValidator.MaxAmountUsd + oneMicroUsd];

    public static TheoryData<DateOnly, DateOnly> OtherMonthSpend() => new() {
        { new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 1) },
        { new DateOnly(2027, 1, 1), new DateOnly(2026, 12, 31) },
    };

    public static TheoryData<decimal, int, decimal> HistoryCosts() => new() {
        { 0.01m, 10, AdUsd },
        { 0.10m, 2, 0.05m },
    };

    public static TheoryData<bool, decimal> AdCosts() => new() {
        { false, AdUsd },
        { true, BatchAdUsd },
    };

    public static TheoryData<decimal> ZeroLimits() => [0m, 0.0000005m];

    public static TheoryData<decimal?, bool> FittingLimits() => new() {
        { AdUsd + CacheWriteUsd + oneMicroUsd, true },
        { 10m, false },
        { null, true },
    };

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldStopCalls_WhenSpendEqualsLimit() {
        bool allowed = budget.Allowed(Config(inFlightUsd + AdUsd), Prices, today, inFlightUsd, AdUsd);

        Assert.False(allowed);
    }

    [Fact]
    public void ShouldReserveCacheWrite_WhenNoCallIsInFlight() {
        bool allowed = budget.Allowed(Config(AdUsd + CacheWriteUsd), Prices, today, 0m, AdUsd);

        Assert.False(allowed);
    }

    [Fact]
    public void ShouldSkipCacheWrite_WhenCallsAreInFlight() {
        bool allowed = budget.Allowed(Config(inFlightUsd + AdUsd + oneMicroUsd), Prices, today, inFlightUsd, AdUsd);

        Assert.True(allowed);
    }

    [Fact]
    public void ShouldFlagCallOverLimit_WhenCallAndCacheWriteReachLimit() {
        bool exceeds = budget.ExceedsLimit(Config(AdUsd + CacheWriteUsd + oneMicroUsd), Prices, today, AdUsd + oneMicroUsd);

        Assert.True(exceeds);
    }

    [Theory]
    [MemberData(nameof(FittingLimits))]
    public void ShouldNotFlagCallOverLimit_WhenCallCanFitLimit(decimal? limit, bool priced) {
        bool exceeds = budget.ExceedsLimit(Config(limit), priced ? Prices : null, today, AdUsd);

        Assert.False(exceeds);
    }

    [Fact]
    public void ShouldNotFlagCallOverLimit_WhenTypicalAdCannotFitLimit() {
        bool exceeds = budget.ExceedsLimit(Config(AdUsd + CacheWriteUsd), Prices, today, 1m);

        Assert.False(exceeds);
    }

    [Fact]
    public void ShouldCountReservation_WhenCallsAreInFlight() {
        Spend(today, 0.05);

        bool allowed = budget.Allowed(Config(0.10m), Prices, today, 0.05m, AdUsd);

        Assert.False(allowed);
    }

    [Fact]
    public void ShouldStopCalls_WhenLimitIsZero() {
        bool allowed = budget.Allowed(Config(0m), Prices, today, 0m, AdUsd);

        Assert.False(allowed);
    }

    [Theory]
    [MemberData(nameof(OutOfRangeAmounts))]
    public void ShouldRejectReservation_WhenOutsideAmountRange(decimal reserved) {
        ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => budget.Allowed(Config(0.10m), Prices, today, reserved, AdUsd));

        Assert.StartsWith("reserved_usd must be between 0 and 1000000 USD", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(OutOfRangeAmounts))]
    public void ShouldRejectCallEstimate_WhenOutsideAmountRange(decimal callUsd) {
        ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => budget.Allowed(Config(0.10m), Prices, today, 0m, callUsd));

        Assert.StartsWith("call_usd must be between 0 and 1000000 USD", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldStopCalls_WhenReservationIsMaxAmount() {
        bool allowed = budget.Allowed(Config(0.10m), Prices, today, AppConfigValidator.MaxAmountUsd, AdUsd);

        Assert.False(allowed);
    }

    [Fact]
    public void ShouldAllowCall_WhenModelIsFreeAndLimitIsUsed() {
        Spend(today, 1.0);

        bool allowed = budget.Allowed(Config(1m), free, today, 0m, AdUsd);

        Assert.True(allowed);
    }

    [Fact]
    public void ShouldStopCalls_WhenOnlyInputIsPriced() {
        bool allowed = budget.Allowed(Config(0m), new ModelPrices(1m, 0m, 0m, 0m, 0m), today, 0m, AdUsd);

        Assert.False(allowed);
    }

    [Fact]
    public void ShouldEstimateAdCost_WhenCheckedCountsPassLongRange() {
        usage.Add(today.AddDays(-3), new UsageCounts(CostUsd: 0.5, AdsChecked: long.MaxValue));
        usage.Add(today.AddDays(-2), new UsageCounts(CostUsd: 0.5, AdsChecked: long.MaxValue));

        decimal estimate = budget.EstimateAdCost(Prices, false, today);

        Assert.Equal(AdUsd, estimate);
    }

    [Fact]
    public void ShouldStopCalls_WhenLimitIsSetAndPriceIsUnknown() {
        bool allowed = budget.Allowed(Config(10m), null, today, 0m, AdUsd);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldNotFlagUnpricedLimit_WhenLimitCanBeChecked(bool limited, bool priced) {
        bool flagged = BudgetEntry.UnpricedLimit(Config(limited ? 1m : null), priced ? Prices : null);

        Assert.False(flagged);
    }

    [Fact]
    public void ShouldAllowCall_WhenNoLimitAndPriceIsUnknown() {
        bool allowed = budget.Allowed(Config(null), null, today, 0m, AdUsd);

        Assert.True(allowed);
    }

    [Theory]
    [MemberData(nameof(OtherMonthSpend))]
    public void ShouldIgnoreSpend_WhenSpentInAnotherMonth(DateOnly spentOn, DateOnly day) {
        Spend(spentOn, 5.0);

        bool allowed = budget.Allowed(Config(1m), Prices, day, 0m, AdUsd);

        Assert.True(allowed);
    }

    [Theory]
    [MemberData(nameof(Affordable), MemberType = typeof(BudgetGolden))]
    public void ShouldEqualGolden_WhenCountingAffordableAds(decimal spent, decimal limit, decimal perAd, int count) {
        Spend(today, (double)spent);

        int? affordable = budget.AffordableCount(Config(limit), Prices, today, perAd);

        Assert.Equal(count, affordable);
    }

    [Fact]
    public void ShouldCapCount_WhenCountPassesIntRange() {
        int? affordable = budget.AffordableCount(Config(AppConfigValidator.MaxAmountUsd), Prices, today, 0m);

        Assert.Equal(int.MaxValue, affordable);
    }

    [Fact]
    public void ShouldAffordNoAds_WhenLimitIsSetAndPriceIsUnknown() {
        int? affordable = budget.AffordableCount(Config(10m), null, today, 0.02m);

        Assert.Equal(0, affordable);
    }

    [Fact]
    public void ShouldNotCapAds_WhenNoLimitIsSet() {
        int? affordable = budget.AffordableCount(Config(null), Prices, today, 0m);

        Assert.Null(affordable);
    }

    [Fact]
    public void ShouldNotCapAds_WhenModelIsFree() {
        Spend(today, 1.0);

        int? affordable = budget.AffordableCount(Config(0.10m), free, today, 0m);

        Assert.Null(affordable);
    }

    [Fact]
    public void ShouldRejectPerAdCost_WhenNegative() {
        ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(
            () => budget.AffordableCount(Config(0.10m), Prices, today, -1m));

        Assert.StartsWith("per_ad_usd must be between", refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AdCosts))]
    public void ShouldEqualGolden_WhenEstimatingAdCost(bool batch, decimal expected) {
        decimal estimate = budget.EstimateAdCost(Prices, batch, today);

        Assert.Equal(expected, estimate);
    }

    [Fact]
    public void ShouldPricePromptAndMaxOutput_WhenEstimatingCall() {
        decimal estimate = budget.EstimateCallCost(Prices, promptCharsPerMtok, oneMtok, today);

        Assert.Equal(Prices.Input + Prices.Output, estimate);
    }

    [Fact]
    public void ShouldReserveAtLeastAdCost_WhenPromptIsShort() {
        decimal estimate = budget.EstimateCallCost(Prices, 0, 0, today);

        Assert.Equal(AdUsd, estimate);
    }

    [Theory]
    [MemberData(nameof(HistoryCosts))]
    public void ShouldUseHigherCost_WhenHistoryIsKnown(decimal cost, int checkedAds, decimal expected) {
        Spend(today.AddDays(-3), (double)cost, checkedAds);

        decimal estimate = budget.EstimateAdCost(Prices, false, today);

        Assert.Equal(expected, estimate);
    }

    [Fact]
    public void ShouldSpreadHistoryCost_WhenSeveralDaysAreChecked() {
        Spend(today.AddDays(-3), 0.10, 1);
        Spend(today.AddDays(-2), 0.30, 3);

        decimal estimate = budget.EstimateAdCost(Prices, false, today);

        Assert.Equal(0.10m, estimate);
    }

    [Fact]
    public void ShouldAverageCheckedDays_WhenCountingAdsPerDay() {
        Spend(today.AddDays(-14), 0.0, 100);
        Spend(today.AddDays(-13), 0.0, 20);
        Spend(today.AddDays(-5), 0.0, 0);
        Spend(today, 0.0, 4);

        decimal ads = budget.AdsPerDay(today);

        Assert.Equal(windowAds, ads);
    }

    [Fact]
    public void ShouldAssumeNineAdsPerDay_WhenNoHistory() {
        decimal ads = budget.AdsPerDay(today);

        Assert.Equal(9m, ads);
    }

    [Theory]
    [MemberData(nameof(AdCosts))]
    public void ShouldPriceEveryAd_WhenEstimatingDailyCost(bool batch, decimal perAd) {
        decimal? cost = BudgetEntry.EstimateDailyCost(Prices, windowAds, batch);

        Assert.Equal(windowAds * perAd, cost);
    }

    [Fact]
    public void ShouldSkipDailyCost_WhenPriceIsUnknown() {
        decimal? cost = BudgetEntry.EstimateDailyCost(null, 9m, false);

        Assert.Null(cost);
    }

    [Fact]
    public void ShouldStoreOneCheckedAd_WhenRecordingSpend() {
        budget.Record(new SpendRecord(today, new TokenCounts(100, 10, 20, 30, 40), 0.25m));

        Assert.Equal([new UsageDay("2026-10-15", 1, 160, 40, 0.25, 0, 1)], usage.Days(today));
    }

    [Fact]
    public void ShouldTotalMonth_WhenRowsSpanTwoMonths() {
        Spend(new DateOnly(2026, 9, 30), 9.0, 1);
        Spend(new DateOnly(2026, 10, 1), 0.5, 1);
        Spend(today, 2.5, 1);

        UsageSummary summary = budget.Summary(Config(10m), today);

        Assert.Equal((3m, 240L, 10m, 30m), (summary.MonthUsd, summary.MonthTokens, summary.LimitUsd, summary.UsedPct));
    }

    [Fact]
    public void ShouldSummarizeWindow_WhenDaysHaveUsage() {
        Spend(new DateOnly(2026, 10, 1), 9.0, 1);
        Spend(today.AddDays(-1), 1.5, 1);
        Spend(today, 0.5, 1);
        Spend(today, 0.5, 1);

        UsageSummary summary = budget.Summary(Config(10m), today);

        Assert.Equal((1m, 240L, 1.25m, 1.5m), (summary.TodayUsd, summary.TodayTokens, summary.AvgUsd, summary.PeakUsd));
        Assert.Equal(new UsageDay("2026-10-02", 0, 0, 0, 0.0, 0, 0), summary.Days[0]);
        Assert.Equal(["2026-10-02", "2026-10-15"], [summary.Days[0].Day, summary.Days[^1].Day]);
    }

    [Fact]
    public void ShouldReportZeroDailyCosts_WhenNoDayHasChecks() {
        Spend(today, 0.5);

        UsageSummary summary = budget.Summary(Config(10m), today);

        Assert.Equal((0m, 0m), (summary.AvgUsd, summary.PeakUsd));
    }

    [Fact]
    public void ShouldLeaveNextMonthOut_WhenSummarizingLastDay() {
        Spend(new DateOnly(2026, 12, 31), 0.90);
        Spend(new DateOnly(2027, 1, 1), 0.05);

        UsageSummary summary = budget.Summary(Config(1m), new DateOnly(2026, 12, 31));

        Assert.Equal((0.9m, 120L), (summary.MonthUsd, summary.MonthTokens));
    }

    [Fact]
    public void ShouldCapTodayTokens_WhenStoredTokensPassLongRange() {
        usage.Add(today, new UsageCounts(InputTokens: long.MaxValue, OutputTokens: 1));

        UsageSummary summary = budget.Summary(Config(1m), today);

        Assert.Equal(long.MaxValue, summary.TodayTokens);
    }

    [Fact]
    public void ShouldCapMonthTokens_WhenStoredTokensPassLongRange() {
        usage.Add(today.AddDays(-1), new UsageCounts(InputTokens: long.MaxValue));
        usage.Add(today, new UsageCounts(InputTokens: long.MaxValue));

        UsageSummary summary = budget.Summary(Config(1m), today);

        Assert.Equal(long.MaxValue, summary.MonthTokens);
    }

    [Fact]
    public void ShouldLeaveUsedShareOut_WhenNoLimitIsSet() {
        UsageSummary summary = budget.Summary(Config(null), today);

        Assert.Null(summary.UsedPct);
    }

    [Theory]
    [MemberData(nameof(ZeroLimits))]
    public void ShouldReportFullUsage_WhenLimitRoundsToZero(decimal limit) {
        UsageSummary summary = budget.Summary(Config(limit), today);

        Assert.Equal(100m, summary.UsedPct);
    }

    [Fact]
    public void ShouldResetOnJanuaryFirst_WhenMonthIsDecember() {
        UsageSummary summary = budget.Summary(Config(1m), new DateOnly(2026, 12, 20));

        Assert.Equal(new DateOnly(2027, 1, 1), summary.ResetsOn);
    }

    private static AppConfig Config(decimal? limit) => new() {
        Profile = new ProfileRef("profile.yaml"),
        Model = new ModelConfig { MonthlyLimitUsd = limit },
    };

    private void Spend(DateOnly day, double cost, int checkedAds = 0) =>
        usage.Add(day, new UsageCounts(Calls: 1, InputTokens: 100, OutputTokens: 20, CostUsd: cost, AdsChecked: checkedAds));
}
