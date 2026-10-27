using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Storage;

internal static class ApplicationRows {
    private const string appliedEvent = "Applied";
    private const string insertSql =
        "INSERT INTO applications (ad_id, company, role, place, deadline, note, applied_at, cv_name) "
        + "VALUES ($ad_id, $company, $role, $place, $deadline, $note, $applied_at, $cv_name) RETURNING id";
    private const string eventSql =
        "INSERT INTO application_events (application_id, at, text) VALUES ($application_id, $at, $text)";
    private const string restoreSql = "UPDATE applications SET removed_at = NULL WHERE id = $id";

    public static long Insert(SqliteConnection connection, SqliteTransaction transaction, NewApplication application) {
        long id = SqlCommands.Scalar(connection, transaction, insertSql,
            ("$ad_id", application.AdId),
            ("$company", application.Company),
            ("$role", application.Role),
            ("$place", application.Place),
            ("$deadline", application.Deadline),
            ("$note", application.Note),
            ("$applied_at", StoredTime.Format(application.AppliedAt)),
            ("$cv_name", application.CvName));
        AddEvent(connection, transaction, id, application.AppliedAt, appliedEvent);
        return id;
    }

    public static void AddEvent(
        SqliteConnection connection, SqliteTransaction transaction, long applicationId, DateTimeOffset at, string text) =>
        SqlCommands.Execute(connection, transaction, eventSql,
            ("$application_id", applicationId), ("$at", StoredTime.Format(at)), ("$text", text));

    public static void Restore(SqliteConnection connection, SqliteTransaction? transaction, long applicationId) =>
        SqlCommands.Execute(connection, transaction, restoreSql, ("$id", applicationId));
}
