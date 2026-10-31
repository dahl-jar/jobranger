using System.Globalization;
using Jobranger.Cli.Config;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Budget;

internal sealed class Budget(SqliteConnection connection) {
    private const long callsPerSpend = 1;
    private const long adsPerCall = 1;
    private const int defaultAdsPerDay = 9;
    private const int recentDays = 14;
    private const decimal microUsdPerUsd = 1_000_000m;
    private const decimal tokensPerMtok = 1_000_000m;
    private const decimal batchMultiplier = 0.5m;
    private const decimal fullPct = 100m;
    private const long prefixTokensMean = 5_816;
    private const long adTokensMean = 1_065;
    private const long outputTokensMean = 1_040;
    private const long adTokensHigh = 2_000;
    private const long outputTokensHigh = 1_600;
    private const int charsPerToken = 2;
    private const string reservedName = "reserved_usd";
    private const string perAdName = "per_ad_usd";

    private static readonly ModelPrices free = new(0m, 0m, 0m, 0m, 0m);

    private readonly UsageStore store = new(connection);

    public static decimal Cost(ModelPrices prices, TokenCounts tokens, bool batch) {
        decimal total = (tokens.Input * prices.Input
            + tokens.CacheWrite5m * prices.CacheWrite5m
            + tokens.CacheWrite1h * prices.CacheWrite1h
            + tokens.CacheRead * prices.CacheRead
            + tokens.Output * prices.Output) / tokensPerMtok;
        return batch ? total * batchMultiplier : total;
    }

    public static decimal? EstimateDailyCost(ModelPrices? prices, decimal dailyAds, bool batch) =>
        prices is null ? null : ConstantAdCost(prices, batch) * dailyAds;

    public static RecheckEstimate EstimateRecheck(int outdatedAds, int systemChars, ModelPrices? prices) {
        if (systemChars < 0) {
            throw new ArgumentOutOfRangeException(nameof(systemChars), string.Create(CultureInfo.InvariantCulture,
                $"system_chars must be zero or more, got {systemChars}"));
        }
        if (outdatedAds == 0) {
            return new RecheckEstimate(0, 0m);
        }
        if (prices is null) {
            return new RecheckEstimate(outdatedAds, null);
        }
        long systemTokens = systemChars / charsPerToken;
        TokenCounts tokens = new(
            adTokensHigh * outdatedAds,
            systemTokens,
            0,
            systemTokens * (outdatedAds - 1),
            outputTokensHigh * outdatedAds);
        return new RecheckEstimate(outdatedAds, Cost(prices, tokens, batch: false));
    }

    public void Record(SpendRecord spend) {
        TokenCounts tokens = spend.Tokens;
        store.Add(spend.Day, new UsageCounts(
            Calls: callsPerSpend,
            InputTokens: tokens.Input + tokens.CacheWrite5m + tokens.CacheWrite1h + tokens.CacheRead,
            OutputTokens: tokens.Output,
            CostUsd: (double)spend.CostUsd,
            AdsChecked: adsPerCall));
    }

    public void RecordRead(DateOnly day, int adsRead) => store.Add(day, new UsageCounts(AdsRead: adsRead));

    public bool Allowed(AppConfig config, ModelPrices? prices, DateOnly today, decimal reservedUsd) {
        RequireAmount(reservedName, reservedUsd);
        if (config.Model.MonthlyLimitUsd is not decimal limit) {
            return true;
        }
        if (prices is null) {
            return false;
        }
        if (IsFree(prices)) {
            return true;
        }
        long cacheWrite = reservedUsd == 0 ? Micros(CacheWriteCost(prices)) : 0;
        long needed = Micros(MonthSpend(today))
            + Micros(reservedUsd)
            + Micros(EstimateAdCost(prices, false, today))
            + cacheWrite;
        return needed < Micros(limit);
    }

    public int? AffordableCount(AppConfig config, ModelPrices? prices, DateOnly today, decimal perAdUsd) {
        RequireAmount(perAdName, perAdUsd);
        if (config.Model.MonthlyLimitUsd is not decimal limit) {
            return null;
        }
        if (prices is null) {
            return 0;
        }
        if (IsFree(prices)) {
            return null;
        }
        long remaining = Micros(limit) - Micros(MonthSpend(today)) - Micros(CacheWriteCost(prices));
        long perAd = Math.Max(1, (long)Math.Ceiling(perAdUsd * microUsdPerUsd));
        long count = Math.Max(0, (remaining - 1) / perAd);
        return (int)Math.Min(count, int.MaxValue);
    }

    public decimal EstimateAdCost(ModelPrices prices, bool batch, DateOnly today) =>
        Math.Max(ConstantAdCost(prices, batch), HistoryAdCost(today));

    public decimal AdsPerDay(DateOnly today) {
        List<UsageDay> days = CheckedDays(RecentDays(today));
        return days.Count > 0 ? days.Average(day => (decimal)day.AdsChecked) : defaultAdsPerDay;
    }

    public UsageSummary Summary(AppConfig config, DateOnly today) {
        DateOnly resets = NextMonthStart(today);
        string resetsDay = StoredTime.Format(resets);
        List<UsageDay> monthDays = [.. store.Days(MonthStart(today))
            .Where(day => string.CompareOrdinal(day.Day, resetsDay) < 0)];
        List<UsageDay> days = RecentDays(today);
        List<decimal> costs = [.. CheckedDays(days).Select(day => (decimal)day.CostUsd)];
        decimal monthUsd = monthDays.Sum(day => (decimal)day.CostUsd);
        decimal? limit = config.Model.MonthlyLimitUsd;
        UsageDay last = days[^1];
        return new UsageSummary(
            monthUsd,
            Tokens(monthDays),
            (decimal)last.CostUsd,
            Tokens([last]),
            costs.DefaultIfEmpty().Average(),
            costs.DefaultIfEmpty().Max(),
            limit,
            UsedPct(monthUsd, limit),
            resets,
            days);
    }

    private static decimal ConstantAdCost(ModelPrices prices, bool batch) => Cost(prices, new TokenCounts(
        adTokensMean,
        0,
        batch ? prefixTokensMean : 0,
        batch ? 0 : prefixTokensMean,
        outputTokensMean), batch);

    private static decimal CacheWriteCost(ModelPrices prices) =>
        Cost(prices, new TokenCounts(0, 0, prefixTokensMean, 0, 0), batch: false);

    private static bool IsFree(ModelPrices prices) => prices == free;

    private static void RequireAmount(string name, decimal usd) {
        if (usd < 0 || usd > AppConfigValidator.MaxAmountUsd) {
            throw new ArgumentOutOfRangeException(name, string.Create(CultureInfo.InvariantCulture,
                $"{name} must be between 0 and {AppConfigValidator.MaxAmountUsd} USD, got {usd}"));
        }
    }

    private static List<UsageDay> CheckedDays(List<UsageDay> days) => [.. days.Where(day => day.AdsChecked > 0)];

    private static long Tokens(IEnumerable<UsageDay> days) =>
        (long)Math.Min(days.Sum(day => (decimal)day.InputTokens + day.OutputTokens), long.MaxValue);

    private static decimal? UsedPct(decimal spentUsd, decimal? limit) {
        if (limit is not decimal limitUsd) {
            return null;
        }
        if (Micros(limitUsd) == 0) {
            return fullPct;
        }
        return spentUsd / limitUsd * fullPct;
    }

    private static DateOnly MonthStart(DateOnly today) => new(today.Year, today.Month, 1);

    private static DateOnly NextMonthStart(DateOnly today) => MonthStart(today).AddMonths(1);

    private static long Micros(decimal usd) => (long)Math.Round(usd * microUsdPerUsd, MidpointRounding.ToEven);

    private decimal MonthSpend(DateOnly today) =>
        (decimal)store.SpendBetween(MonthStart(today), NextMonthStart(today));

    private decimal HistoryAdCost(DateOnly today) {
        List<UsageDay> days = CheckedDays(RecentDays(today));
        decimal checkedAds = days.Sum(day => (decimal)day.AdsChecked);
        return checkedAds > 0 ? days.Sum(day => (decimal)day.CostUsd) / checkedAds : 0m;
    }

    private List<UsageDay> RecentDays(DateOnly today) {
        DateOnly first = today.AddDays(1 - recentDays);
        Dictionary<string, UsageDay> stored = store.Days(first).ToDictionary(day => day.Day, StringComparer.Ordinal);
        return [.. Enumerable.Range(0, recentDays)
            .Select(offset => StoredTime.Format(first.AddDays(offset)))
            .Select(day => stored.GetValueOrDefault(day) ?? new UsageDay(day, 0, 0, 0, 0, 0, 0))];
    }
}
