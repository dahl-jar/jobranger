using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Ads;

internal sealed partial class AdStore {
    private const string upsertActionSql =
        "INSERT INTO ad_actions (ad_id, action, at) VALUES ($id, $action, $at) "
        + "ON CONFLICT (ad_id, action) DO UPDATE SET at = excluded.at";
    private const string deleteActionSql = "DELETE FROM ad_actions WHERE ad_id = $id AND action = $action";
    private const string removeApplicationSql =
        "UPDATE applications SET removed_at = $at WHERE ad_id = $id AND removed_at IS NULL";
    private const string applicationForAdSql = "SELECT id FROM applications WHERE ad_id = $id";
    private const string actionsSql = "SELECT ad_id, action FROM ad_actions";

    private static readonly AdAction[] exclusiveActions = [AdAction.Applied, AdAction.Skipped, AdAction.NotForMe];

    private static readonly Dictionary<AdAction, string> actionNames = new() {
        [AdAction.Applied] = "applied",
        [AdAction.Skipped] = "skipped",
        [AdAction.NotForMe] = "not_for_me",
        [AdAction.ShowAnyway] = "show_anyway",
    };

    private static readonly Dictionary<string, AdAction> actionsByName =
        actionNames.ToDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public void MarkAd(string adId, AdAction action, DateTimeOffset at, string? cvName = null) {
        string stamp = StoredTime.Format(at);
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        if (exclusiveActions.Contains(action)) {
            foreach (AdAction other in exclusiveActions.Where(other => other != action)) {
                DeleteAction(transaction, adId, other, stamp);
            }
        }
        SqlCommands.Execute(connection, transaction, upsertActionSql,
            ("$id", adId), ("$action", actionNames[action]), ("$at", stamp));
        if (action == AdAction.Applied) {
            ApplyFor(transaction, adId, at, cvName);
        }
        transaction.Commit();
    }

    public void UnmarkAd(string adId, AdAction action, DateTimeOffset at) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        DeleteAction(transaction, adId, action, StoredTime.Format(at));
        transaction.Commit();
    }

    public IReadOnlyDictionary<string, IReadOnlySet<AdAction>> Actions() {
        using SqliteCommand command = SqlCommands.Create(connection, null, actionsSql);
        using SqliteDataReader reader = command.ExecuteReader();
        Dictionary<string, HashSet<AdAction>> found = new(StringComparer.Ordinal);
        while (reader.Read()) {
            string adId = reader.GetString(0);
            if (!found.TryGetValue(adId, out HashSet<AdAction>? marks)) {
                marks = [];
                found[adId] = marks;
            }
            marks.Add(actionsByName[reader.GetString(1)]);
        }
        return found.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<AdAction>)pair.Value, StringComparer.Ordinal);
    }

    public IReadOnlySet<string> ShownAnyway() => ShownAnyway(Actions());

    public IReadOnlyList<Ad> KeepReportable(IReadOnlyList<Ad> ads, Func<Ad, bool> isReportable) {
        IReadOnlyDictionary<string, IReadOnlySet<AdAction>> actions = Actions();
        HashSet<string> shown = ShownAnyway(actions);
        return [.. ads.Where(ad => !IsDismissed(actions, ad.Id) && (shown.Contains(ad.Id) || isReportable(ad)))];
    }

    private static AdAction? YourAction(IReadOnlyDictionary<string, IReadOnlySet<AdAction>> actions, string adId) =>
        actions.TryGetValue(adId, out IReadOnlySet<AdAction>? marks)
            ? exclusiveActions.Where(marks.Contains).Cast<AdAction?>().FirstOrDefault()
            : null;

    private static bool IsDismissed(IReadOnlyDictionary<string, IReadOnlySet<AdAction>> actions, string adId) =>
        YourAction(actions, adId) is not null;

    private static HashSet<string> ShownAnyway(IReadOnlyDictionary<string, IReadOnlySet<AdAction>> actions) =>
        actions.Where(pair => pair.Value.Contains(AdAction.ShowAnyway)).Select(pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);

    private void DeleteAction(SqliteTransaction transaction, string adId, AdAction action, string at) {
        int deleted = SqlCommands.Execute(connection, transaction, deleteActionSql,
            ("$id", adId), ("$action", actionNames[action]));
        if (action == AdAction.Applied && deleted > 0) {
            SqlCommands.Execute(connection, transaction, removeApplicationSql, ("$at", at), ("$id", adId));
        }
    }

    private void ApplyFor(SqliteTransaction transaction, string adId, DateTimeOffset at, string? cvName) {
        using (SqliteCommand existing = SqlCommands.Create(connection, transaction, applicationForAdSql, ("$id", adId))) {
            if (existing.ExecuteScalar() is long applicationId) {
                ApplicationRows.Restore(connection, transaction, applicationId);
                return;
            }
        }
        Ad ad = Select(transaction, byIdWhere, idOrdering, null, ("$ids", JsonLists.Write([adId]))).Single();
        ApplicationRows.Insert(connection, transaction, new NewApplication(
            ad.Company, ad.DisplayTitle ?? ad.Title, ad.Location, ad.Deadline, "", at, cvName, adId));
    }
}
