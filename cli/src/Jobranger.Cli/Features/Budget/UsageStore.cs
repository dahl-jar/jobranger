using System.Globalization;
using Jobranger.Cli.Config;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Budget;

internal sealed class UsageStore(SqliteConnection connection) : IUsageStore {
    private const string addSql =
        "INSERT INTO usage_days (day, calls, input_tokens, output_tokens, cost_usd, ads_read, ads_checked) "
        + "VALUES ($day, $calls, $input_tokens, $output_tokens, $cost_usd, $ads_read, $ads_checked) "
        + "ON CONFLICT (day) DO UPDATE SET calls = calls + excluded.calls, "
        + "input_tokens = input_tokens + excluded.input_tokens, "
        + "output_tokens = output_tokens + excluded.output_tokens, "
        + "cost_usd = cost_usd + excluded.cost_usd, "
        + "ads_read = ads_read + excluded.ads_read, "
        + "ads_checked = ads_checked + excluded.ads_checked";
    private const string costSql = "MIN(MAX(CAST(cost_usd AS REAL), 0), $max_cost)";
    private const string daysSql =
        "SELECT day, " + costSql + ", MAX(CAST(calls AS INTEGER), 0), MAX(CAST(input_tokens AS INTEGER), 0), "
        + "MAX(CAST(output_tokens AS INTEGER), 0), MAX(CAST(ads_read AS INTEGER), 0), "
        + "MAX(CAST(ads_checked AS INTEGER), 0) FROM usage_days WHERE day >= $since ORDER BY day";
    private const string spendSql =
        "SELECT COALESCE(SUM(" + costSql + "), 0) FROM usage_days WHERE day >= $start AND day < $end";

    private static readonly double maxCost = (double)AppConfigValidator.MaxAmountUsd;

    public void Add(DateOnly day, UsageCounts counts) {
        if (!double.IsFinite(counts.CostUsd) || counts.CostUsd < 0) {
            throw new ArgumentOutOfRangeException(nameof(counts), string.Create(CultureInfo.InvariantCulture,
                $"cost_usd must be a finite amount of at least 0, got {counts.CostUsd}"));
        }
        SqlCommands.Execute(connection, null, addSql,
            ("$day", StoredTime.Format(day)),
            ("$calls", counts.Calls),
            ("$input_tokens", counts.InputTokens),
            ("$output_tokens", counts.OutputTokens),
            ("$cost_usd", counts.CostUsd),
            ("$ads_read", counts.AdsRead),
            ("$ads_checked", counts.AdsChecked));
    }

    public IReadOnlyList<UsageDay> Days(DateOnly since) =>
        SqlCommands.Query(connection, null, daysSql, ReadDay, ("$max_cost", maxCost), ("$since", StoredTime.Format(since)));

    public double SpendBetween(DateOnly start, DateOnly end) {
        using SqliteCommand command = SqlCommands.Create(connection, null, spendSql,
            ("$max_cost", maxCost), ("$start", StoredTime.Format(start)), ("$end", StoredTime.Format(end)));
        return Convert.ToDouble(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static UsageDay ReadDay(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetInt64(2),
        reader.GetInt64(3),
        reader.GetInt64(4),
        reader.GetDouble(1),
        reader.GetInt64(5),
        reader.GetInt64(6));
}
