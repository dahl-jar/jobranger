using Jobranger.Cli.Config;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Runs;

internal sealed class RunStore(SqliteConnection connection) : IRunStore {
    private const string failStaleSql =
        "UPDATE runs SET status = 'failed', finished_at = $at WHERE status = 'running'";
    private const string startSql = "INSERT INTO runs (started_at, status) VALUES ($at, 'running') RETURNING id";
    private const string finishSql =
        "UPDATE runs SET finished_at = $finished_at, status = $status, ads_read = $ads_read, "
        + "new_ads = $new_ads, matches = $matches, hidden = $hidden, failed_sources = $failed_sources WHERE id = $id";
    private const string listSql =
        "SELECT id, started_at, finished_at, status, ads_read, new_ads, matches, hidden, failed_sources "
        + "FROM runs ORDER BY started_at DESC, id DESC LIMIT $limit";

    public long Start(DateTimeOffset startedAt) {
        string at = StoredTime.Format(startedAt);
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        SqlCommands.Execute(connection, transaction, failStaleSql, ("$at", at));
        long id = SqlCommands.Scalar(connection, transaction, startSql, ("$at", at));
        transaction.Commit();
        return id;
    }

    public void Finish(long runId, RunSummary summary) =>
        SqlCommands.Execute(connection, null, finishSql,
            ("$finished_at", StoredTime.Format(summary.FinishedAt)),
            ("$status", WireNames.Of(summary.Status)),
            ("$ads_read", summary.AdsRead),
            ("$new_ads", summary.NewAds),
            ("$matches", summary.Matches),
            ("$hidden", summary.Hidden),
            ("$failed_sources", JsonLists.Write(summary.FailedSources)),
            ("$id", runId));

    public RunRow? Last() => List(1) is [RunRow last] ? last : null;

    public IReadOnlyList<RunRow> List(int limit) => SqlCommands.Query(connection, null, listSql, ReadRun, ("$limit", limit));

    private static RunRow ReadRun(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.OptionalText(2),
        WireNames.Parse<RunStatus>(reader.GetString(3)),
        reader.GetInt64(4),
        reader.GetInt64(5),
        reader.GetInt64(6),
        reader.GetInt64(7),
        JsonLists.Read(reader.OptionalText(8)));
}
