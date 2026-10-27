using Jobranger.Cli.Config;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Applications;

internal sealed class ApplicationStore(SqliteConnection connection) : IApplicationStore {
    private const string statusSql = "UPDATE applications SET status = $status WHERE id = $id";
    private const string noteSql = "UPDATE applications SET note = $note WHERE id = $id";
    private const string removeSql = "UPDATE applications SET removed_at = $at WHERE id = $id";
    private const string listSql =
        "SELECT id, ad_id, company, role, place, deadline, status, note, applied_at, cv_name, removed_at "
        + "FROM applications";
    private const string notRemovedWhere = " WHERE removed_at IS NULL";
    private const string listOrder = " ORDER BY applied_at DESC, id DESC";
    private const string eventsSql = "SELECT application_id, at, text FROM application_events ORDER BY at, id";
    private const string statusEventPrefix = "Status: ";

    public long Create(NewApplication application) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        long id = ApplicationRows.Insert(connection, transaction, application);
        transaction.Commit();
        return id;
    }

    public void Update(long id, ApplicationChange change, DateTimeOffset at) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        if (change.Status is ApplicationStatus status) {
            SqlCommands.Execute(connection, transaction, statusSql, ("$status", WireNames.Of(status)), ("$id", id));
            ApplicationRows.AddEvent(connection, transaction, id, at, statusEventPrefix + status);
        }
        if (change.Note is string note) {
            SqlCommands.Execute(connection, transaction, noteSql, ("$note", note), ("$id", id));
        }
        transaction.Commit();
    }

    public void Remove(long id, DateTimeOffset at) =>
        SqlCommands.Execute(connection, null, removeSql, ("$at", StoredTime.Format(at)), ("$id", id));

    public void Restore(long id) => ApplicationRows.Restore(connection, null, id);

    public void AddEvent(long id, DateTimeOffset at, string text) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        ApplicationRows.AddEvent(connection, transaction, id, at, text);
        transaction.Commit();
    }

    public IReadOnlyList<Application> List(bool includeRemoved = false) {
        Dictionary<long, List<ApplicationEvent>> events = Events();
        string sql = listSql + (includeRemoved ? "" : notRemovedWhere) + listOrder;
        return SqlCommands.Query(connection, null, sql, reader => ReadApplication(reader, events));
    }

    private static Application ReadApplication(
        SqliteDataReader reader, Dictionary<long, List<ApplicationEvent>> events) {
        long id = reader.GetInt64(0);
        return new Application(
            id,
            reader.OptionalText(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.OptionalText(5),
            WireNames.Parse<ApplicationStatus>(reader.GetString(6)),
            reader.GetString(7),
            reader.GetString(8),
            reader.OptionalText(9),
            reader.OptionalText(10),
            events.TryGetValue(id, out List<ApplicationEvent>? found) ? found : []);
    }

    private Dictionary<long, List<ApplicationEvent>> Events() =>
        SqlCommands.Query(connection, null, eventsSql,
                reader => (Id: reader.GetInt64(0), Event: new ApplicationEvent(reader.GetString(1), reader.GetString(2))))
            .GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Event).ToList());
}
