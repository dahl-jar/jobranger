using System.Globalization;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Learning;

internal sealed class DecisionStore(SqliteConnection connection) : IDecisionStore {
    private const string columns =
        "id, ad_id, dedup_key, action, decided_at, title, company, summary, requirements, embed_text, embedded_model";
    private const string recordSql =
        "INSERT INTO decisions (ad_id, dedup_key, action, decided_at, title, company, summary, requirements, embed_text) "
        + "VALUES ($ad_id, $dedup_key, $action, $decided_at, $title, $company, $summary, $requirements, $embed_text) "
        + "ON CONFLICT (ad_id) DO UPDATE SET dedup_key = excluded.dedup_key, action = excluded.action, "
        + "decided_at = excluded.decided_at, title = excluded.title, company = excluded.company, "
        + "summary = excluded.summary, requirements = excluded.requirements, embed_text = excluded.embed_text, "
        + "embedded_model = NULL RETURNING id";
    private const string dropVectorSql = "DELETE FROM decision_vectors WHERE decision_id = $id";
    private const string withdrawSql = "DELETE FROM decisions WHERE ad_id = $ad_id RETURNING id";
    private const string deleteSql = "DELETE FROM decisions WHERE id IN (SELECT value FROM json_each($ids))";
    private const string allIdsSql = "SELECT id FROM decisions";
    private const string requeueSql =
        "UPDATE ads SET " + ClassificationReset.Columns + " "
        + "WHERE active = 1 AND id IN (SELECT ad_id FROM classification_examples "
        + "WHERE decision_id IN (SELECT value FROM json_each($ids))) "
        + "AND NOT EXISTS (SELECT 1 FROM ad_actions WHERE ad_actions.ad_id = ads.id) RETURNING id";
    private const string requeueAdSql = "UPDATE ads SET " + ClassificationReset.Columns + " WHERE id = $ad_id";
    private const string dropExamplesSql =
        "DELETE FROM classification_examples WHERE ad_id IN (SELECT value FROM json_each($ad_ids))";
    private const string addExampleSql =
        "INSERT INTO classification_examples (ad_id, decision_id) "
        + "SELECT $ad_id, id FROM decisions WHERE id = $decision_id";
    private const string everyWhere = "TRUE";
    private const string byIdWhere = "id IN (SELECT value FROM json_each($ids))";
    private const string pendingWhere = "embedded_model IS NOT $model";
    private const string newestFirst = "decided_at DESC, id DESC";
    private const string idOrdering = "id";
    private const string setEmbeddedSql =
        "UPDATE decisions SET embedded_model = $model WHERE id = $id AND embed_text = $embed_text";
    private const string countSql = "SELECT COUNT(*) FROM decisions";
    private const string useCountsSql =
        "SELECT decision_id, COUNT(*) FROM classification_examples GROUP BY decision_id ORDER BY decision_id";

    private static readonly Dictionary<DecisionAction, string> actionNames = new() {
        [DecisionAction.Applied] = "applied",
        [DecisionAction.NotForMe] = "not_for_me",
        [DecisionAction.ShowAnyway] = "show_anyway",
    };

    private static readonly Dictionary<string, DecisionAction> actionsByName =
        actionNames.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    private readonly VectorIndex vectors = new(connection);

    public long Record(NewDecision decision) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        long id = SqlCommands.Scalar(connection, transaction, recordSql,
            ("$ad_id", decision.AdId),
            ("$dedup_key", decision.DedupKey),
            ("$action", actionNames[decision.Action]),
            ("$decided_at", StoredTime.Format(decision.DecidedAt)),
            ("$title", decision.Title),
            ("$company", decision.Company),
            ("$summary", decision.Summary),
            ("$requirements", JsonLists.Write(decision.Requirements)),
            ("$embed_text", decision.EmbedText));
        SqlCommands.Execute(connection, transaction, dropVectorSql, ("$id", id));
        transaction.Commit();
        return id;
    }

    public long? Withdraw(string adId) {
        using SqliteCommand command = SqlCommands.Create(connection, null, withdrawSql, ("$ad_id", adId));
        return command.ExecuteScalar() as long?;
    }

    public IReadOnlyList<string>? Forget(long decisionId) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        string ids = IdList([decisionId]);
        IReadOnlyList<string> requeued = Requeue(transaction, ids);
        if (SqlCommands.Execute(connection, transaction, deleteSql, ("$ids", ids)) == 0) {
            return null;
        }
        transaction.Commit();
        return requeued;
    }

    public IReadOnlyList<string> ForgetAll() {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        string ids = IdList(SqlCommands.Query(connection, transaction, allIdsSql, reader => reader.GetInt64(0)));
        IReadOnlyList<string> requeued = Requeue(transaction, ids);
        SqlCommands.Execute(connection, transaction, deleteSql, ("$ids", ids));
        transaction.Commit();
        return requeued;
    }

    public IReadOnlyList<Decision> List() => Select(everyWhere, newestFirst);

    public IReadOnlyDictionary<long, Decision> ById(IEnumerable<long> decisionIds) =>
        Select(byIdWhere, idOrdering, ("$ids", IdList(decisionIds))).ToDictionary(decision => decision.Id);

    public IReadOnlyList<Decision> PendingEmbeddings(string model) =>
        Select(pendingWhere, idOrdering, ("$model", model));

    public bool StoreEmbedding(long decisionId, string embedText, string model, float[] vector) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        if (SqlCommands.Execute(connection, transaction, setEmbeddedSql,
            ("$model", model), ("$id", decisionId), ("$embed_text", embedText)) != 1) {
            return false;
        }
        vectors.Add(transaction, decisionId, vector);
        transaction.Commit();
        return true;
    }

    public long Count() => SqlCommands.Scalar(connection, null, countSql);

    public IReadOnlyDictionary<long, int> UseCounts() =>
        SqlCommands.Query(connection, null, useCountsSql, reader => (Id: reader.GetInt64(0), Uses: reader.GetInt32(1)))
            .ToDictionary(row => row.Id, row => row.Uses);

    public bool RecordExamples(string adId, IReadOnlyList<long> decisionIds) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        string adIds = JsonLists.Write([adId]);
        SqlCommands.Execute(connection, transaction, dropExamplesSql, ("$ad_ids", adIds));
        int added = 0;
        foreach (long decisionId in decisionIds) {
            added += SqlCommands.Execute(connection, transaction, addExampleSql,
                ("$ad_id", adId), ("$decision_id", decisionId));
        }
        bool missing = added < decisionIds.Count;
        if (missing) {
            SqlCommands.Execute(connection, transaction, requeueAdSql, ("$ad_id", adId));
            SqlCommands.Execute(connection, transaction, dropExamplesSql, ("$ad_ids", adIds));
        }
        transaction.Commit();
        return missing;
    }

    private static string IdList(IEnumerable<long> ids) =>
        "[" + string.Join(',', ids.Select(id => id.ToString(CultureInfo.InvariantCulture))) + "]";

    private static Decision ReadDecision(SqliteDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetString(1),
        reader.GetString(2),
        actionsByName[reader.GetString(3)],
        reader.GetString(4),
        reader.GetString(5),
        reader.GetString(6),
        reader.GetString(7),
        JsonLists.Read(reader.GetString(8)),
        reader.GetString(9),
        reader.OptionalText(10));

    private List<string> Requeue(SqliteTransaction transaction, string ids) {
        List<string> requeued =
            SqlCommands.Query(connection, transaction, requeueSql, reader => reader.GetString(0), ("$ids", ids));
        SqlCommands.Execute(connection, transaction, dropExamplesSql, ("$ad_ids", JsonLists.Write(requeued)));
        return requeued;
    }

    private List<Decision> Select(
        string where,
        string ordering,
        params ReadOnlySpan<(string Name, object? Value)> values) =>
        SqlCommands.Query(connection, null,
            "SELECT " + columns + " FROM decisions WHERE " + where + " ORDER BY " + ordering, ReadDecision, values);
}
