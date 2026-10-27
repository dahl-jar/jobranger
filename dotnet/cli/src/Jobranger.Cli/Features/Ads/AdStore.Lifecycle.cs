using System.Globalization;
using Jobranger.Cli.Config;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Ads;

internal sealed partial class AdStore {
    public const int ClosedKeepDays = 30;

    private const string closeColumns =
        "active = 0, closed_at = COALESCE(closed_at, $now), closed_reason = COALESCE(closed_reason, 'removed')";
    private const string reopenColumns = "active = 1, closed_at = NULL, closed_reason = NULL";
    private const string reopenSql = "UPDATE ads SET " + reopenColumns + " WHERE id = $id AND active = 0";
    private const string closeSql = "UPDATE ads SET " + closeColumns + " WHERE id = $id AND active = 1";
    private const string syncSourceSql =
        "UPDATE ads SET " + closeColumns + " WHERE source = $source AND active = 1 "
        + "AND id NOT IN (SELECT value FROM json_each($listed))";
    private const string reopenListedSql =
        "UPDATE ads SET " + reopenColumns + " WHERE active = 0 AND id IN (SELECT value FROM json_each($listed))";
    private const string unlistSql = "UPDATE ads SET category_listed = 0 WHERE id = $id";
    private const string listSql = "UPDATE ads SET category_listed = 1 WHERE id = $id";
    private const string syncFinnSql =
        "UPDATE ads SET " + closeColumns + " WHERE source = 'finn' AND active = 1 "
        + "AND (category_listed = 1 OR ($window IS NOT NULL AND published >= $window)) "
        + "AND id NOT IN (SELECT value FROM json_each($listed))";
    private const string closePastDeadlineSql =
        "UPDATE ads SET closed_at = $as_of, closed_reason = 'deadline' "
        + "WHERE active = 1 AND closed_at IS NULL AND deadline IS NOT NULL "
        + "AND date(deadline) IS NOT NULL AND date(deadline) < date($as_of)";
    private const string closedSql =
        "SELECT id, closed_at, closed_reason FROM ads WHERE closed_at IS NOT NULL "
        + "AND date(closed_at) >= date($as_of, $keep) ORDER BY closed_at DESC, id";
    private const string closedSinceSql = "SELECT COUNT(*) FROM ads WHERE closed_at >= $since";
    private const string keepDaysPrefix = "-";
    private const string keepDaysSuffix = " days";
    private const int closedOnLength = 10;

    public void SetActive(string adId, bool active) {
        if (active) {
            SqlCommands.Execute(connection, null, reopenSql, ("$id", adId));
        } else {
            SqlCommands.Execute(connection, null, closeSql, ("$now", Now()), ("$id", adId));
        }
    }

    public void SyncSourceActive(string source, IEnumerable<string> activeIds) {
        string listed = JsonLists.Write(activeIds);
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        SqlCommands.Execute(connection, transaction, syncSourceSql,
            ("$now", Now()), ("$source", source), ("$listed", listed));
        SqlCommands.Execute(connection, transaction, reopenListedSql, ("$listed", listed));
        transaction.Commit();
    }

    public void MarkCategoryUnlisted(IReadOnlyList<string> adIds) => ForEachId(unlistSql, adIds);

    public void SyncFinnActive(
        IReadOnlyCollection<string> categoryIds, IReadOnlyCollection<string> recentIds, DateTimeOffset? windowStart) {
        string listed = JsonLists.Write(categoryIds.Union(recentIds, StringComparer.Ordinal));
        string? window = windowStart is DateTimeOffset start ? StoredTime.Format(start) : null;
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        foreach (string adId in categoryIds) {
            SqlCommands.Execute(connection, transaction, listSql, ("$id", adId));
        }
        SqlCommands.Execute(connection, transaction, syncFinnSql,
            ("$now", Now()), ("$window", window), ("$listed", listed));
        SqlCommands.Execute(connection, transaction, reopenListedSql, ("$listed", listed));
        transaction.Commit();
    }

    public int ClosePastDeadline(DateOnly asOf) =>
        SqlCommands.Execute(connection, null, closePastDeadlineSql, ("$as_of", StoredTime.Format(asOf)));

    public IReadOnlyList<ClosedAd> Closed(DateOnly asOf, int keepDays = ClosedKeepDays) {
        List<(string Id, string ClosedAt, string? Reason)> rows = SqlCommands.Query(connection, null, closedSql,
            reader => (reader.GetString(0), reader.GetString(1), reader.OptionalText(2)),
            ("$as_of", StoredTime.Format(asOf)),
            ("$keep", keepDaysPrefix + keepDays.ToString(CultureInfo.InvariantCulture) + keepDaysSuffix));
        Dictionary<string, Ad> ads = ById(rows.Select(row => row.Id)).ToDictionary(ad => ad.Id, StringComparer.Ordinal);
        IReadOnlyDictionary<string, IReadOnlySet<AdAction>> actions = Actions();
        return [.. rows.Select(row => new ClosedAd(
            ads[row.Id],
            row.ClosedAt[..Math.Min(closedOnLength, row.ClosedAt.Length)],
            row.Reason is string reason ? WireNames.Parse<ClosedReason>(reason) : ClosedReason.Removed,
            YourAction(actions, row.Id)))];
    }

    public long ClosedSince(string since) => SqlCommands.Scalar(connection, null, closedSinceSql, ("$since", since));
}
