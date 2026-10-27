using Jobranger.Cli.Config;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Ads;

internal sealed partial class AdStore(SqliteConnection connection, TimeProvider time) : IAdStore {
    public const int DescriptionMaxAttempts = 3;

    private const string sharedColumns =
        "id, source, source_id, title, company, location, "
        + "published, deadline, url, description, label, summary, requires_master, "
        + "is_programming_job, display_title, role_type, technologies, requirements, "
        + "assessment, apply_url, apply_status, apply_checked_at, republished";
    private const string adColumns = sharedColumns + ", first_seen, closed_at, first_classified_at, assessment_version";
    private const string insertSql =
        "INSERT INTO ads (" + sharedColumns + ", assessment_version, dedup_key, first_seen) VALUES ("
        + "$id, $source, $source_id, $title, $company, $location, $published, $deadline, $url, $description, "
        + "$label, $summary, $requires_master, $is_programming_job, $display_title, $role_type, $technologies, "
        + "$requirements, $assessment, $apply_url, $apply_status, $apply_checked_at, $republished, "
        + "$assessment_version, $dedup_key, $first_seen) ON CONFLICT (source, source_id) DO NOTHING";
    private const string outdatedWhere = "active = 1 AND assessment_version IS NOT $version";
    private const string pendingDescriptionWhere =
        "description IS NULL AND active = 1 "
        + "AND (label IS NULL OR label NOT IN (SELECT value FROM json_each($labels))) "
        + "AND description_attempts < $attempts";
    private const string bumpDescriptionSql =
        "UPDATE ads SET description_attempts = description_attempts + 1 WHERE id = $id";
    private const string setDescriptionSql =
        "UPDATE ads SET description = $text, apply_url = COALESCE($apply_url, apply_url) WHERE id = $id";
    private const string expiredApplySql =
        "UPDATE ads SET apply_status = $status, apply_checked_at = $at, "
        + "closed_at = COALESCE(closed_at, $at), "
        + "closed_reason = COALESCE(closed_reason, 'filled') WHERE id = $id";
    private const string applyStatusSql =
        "UPDATE ads SET apply_status = $status, apply_checked_at = $at WHERE id = $id";
    private const string pendingApplyUrlWhere =
        "source = $source AND active = 1 AND " + targetWhere + " "
        + "AND description IS NOT NULL AND apply_url IS NULL "
        + "AND apply_status IS NULL AND description_attempts < $attempts";
    private const string pendingApplyCheckWhere =
        "active = 1 AND " + targetWhere + " AND apply_url IS NOT NULL "
        + "AND (deadline IS NULL OR date(deadline) IS NULL OR date(deadline) >= date($as_of)) "
        + "AND (apply_checked_at IS NULL OR apply_checked_at < $recheck_before) "
        + "AND (apply_status IS NULL OR apply_status NOT IN ('expired', 'skipped'))";
    private const string targetWhere =
        "is_programming_job = 1 "
        + "AND (label IS NULL OR label IN (SELECT value FROM json_each($target_labels))) "
        + "AND role_type IN (SELECT value FROM json_each($target_roles))";
    private const string noMastersWhere = " AND requires_master = 0";
    private const string pendingClassificationWhere =
        "description IS NOT NULL AND active = 1 AND label IS NULL AND label_attempts < $attempts";
    private const string exhaustedClassificationWhere =
        "description IS NOT NULL AND active = 1 AND label IS NULL AND label_attempts >= $attempts";
    private const string bumpLabelSql = "UPDATE ads SET label_attempts = label_attempts + 1 WHERE id = $id";
    private const string setClassificationSql =
        "UPDATE ads SET label = $label, summary = $summary, requires_master = $requires_master, "
        + "is_programming_job = $is_programming_job, display_title = $display_title, role_type = $role_type, "
        + "technologies = $technologies, requirements = $requirements, assessment = $assessment, "
        + "assessment_version = $version, classified_at = $at, "
        + "first_classified_at = COALESCE(first_classified_at, $at) WHERE id = $id";
    private const string setLabelSql =
        "UPDATE ads SET label = $label, is_programming_job = 0, assessment = NULL, classified_at = NULL, "
        + "assessment_version = $version WHERE id = $id";
    private const string knownIdsSql = "SELECT id FROM ads";
    private const string newSinceWhere = "first_seen >= $since";
    private const string reportableWhere =
        "active = 1 AND reported_at IS NULL AND "
        + "(deadline IS NULL OR date(deadline) IS NULL OR date(deadline) >= date($as_of)) "
        + "AND (apply_status IS NULL OR apply_status != 'expired')";
    private const string reportedSinceWhere = "reported_at >= $since";
    private const string markReportedSql = "UPDATE ads SET reported_at = $at WHERE id = $id";
    private const string activeWhere = "active = 1";
    private const string byIdWhere = "id IN (SELECT value FROM json_each($ids))";
    private const string refreshSql =
        "UPDATE ads SET label = NULL, summary = NULL, requires_master = 0, "
        + "is_programming_job = NULL, display_title = NULL, role_type = NULL, "
        + "technologies = NULL, requirements = NULL, label_attempts = 0, "
        + "assessment = NULL, assessment_version = $version, classified_at = NULL WHERE " + outdatedWhere;
    private const string countOutdatedSql = "SELECT COUNT(*) FROM ads WHERE " + outdatedWhere;
    private const string idOrdering = "id";
    private const string newestFirst = "first_seen DESC";
    private const string uncheckedFirst = "apply_checked_at IS NOT NULL, first_seen DESC";
    private const string expiredStatus = "expired";
    private const string generalSoftware = "general_software";

    private static readonly Dictionary<string, string> levelsFromLabel = new(StringComparer.Ordinal) {
        ["junior"] = "junior",
        ["mid"] = "mid",
        ["senior"] = "senior",
        ["lead"] = "lead",
        ["none"] = "none",
        ["error"] = "none",
    };

    public bool Upsert(Ad ad) => SqlCommands.Execute(connection, null, insertSql,
        ("$id", ad.Id),
        ("$source", ad.Source),
        ("$source_id", ad.SourceId),
        ("$title", ad.Title),
        ("$company", ad.Company),
        ("$location", ad.Location),
        ("$published", ad.Published),
        ("$deadline", ad.Deadline),
        ("$url", ad.Url),
        ("$description", ad.Description),
        ("$label", ad.Label),
        ("$summary", ad.Summary),
        ("$requires_master", ad.RequiresMaster),
        ("$is_programming_job", ad.IsProgrammingJob),
        ("$display_title", ad.DisplayTitle),
        ("$role_type", ad.RoleType),
        ("$technologies", JsonLists.Write(ad.Technologies)),
        ("$requirements", JsonLists.Write(ad.Requirements)),
        ("$assessment", ad.Assessment),
        ("$apply_url", ad.ApplyUrl),
        ("$apply_status", ad.ApplyStatus),
        ("$apply_checked_at", ad.ApplyCheckedAt),
        ("$republished", ad.Republished),
        ("$assessment_version", ad.AssessmentVersion),
        ("$dedup_key", DedupKey.For(ad.Company, ad.Title)),
        ("$first_seen", Now())) == 1;

    public IReadOnlyList<Ad> PendingDescription(IEnumerable<string> skipLabels) =>
        Select(pendingDescriptionWhere, ("$labels", JsonLists.Write(skipLabels)), ("$attempts", DescriptionMaxAttempts));

    public void BumpDescriptionAttempts(string adId) =>
        SqlCommands.Execute(connection, null, bumpDescriptionSql, ("$id", adId));

    public void SetDescription(string adId, string text, string? applyUrl) =>
        SqlCommands.Execute(connection, null, setDescriptionSql, ("$text", text), ("$apply_url", applyUrl), ("$id", adId));

    public void SetApplyStatus(string adId, string status, DateTimeOffset checkedAt) =>
        SqlCommands.Execute(connection, null, status == expiredStatus ? expiredApplySql : applyStatusSql,
            ("$status", status), ("$at", StoredTime.Format(checkedAt)), ("$id", adId));

    public IReadOnlyList<Ad> PendingApplyUrl(string source, int limit, Targets targets) =>
        Select(null, TargetFiltered(pendingApplyUrlWhere, targets), newestFirst, limit,
            ("$source", source),
            ("$attempts", DescriptionMaxAttempts),
            ("$target_labels", TargetLabels(targets)),
            ("$target_roles", TargetRoles(targets)));

    public IReadOnlyList<Ad> PendingApplyCheck(DateOnly asOf, DateTimeOffset recheckBefore, int limit, Targets targets) =>
        Select(null, TargetFiltered(pendingApplyCheckWhere, targets), uncheckedFirst, limit,
            ("$as_of", StoredTime.Format(asOf)),
            ("$recheck_before", StoredTime.Format(recheckBefore)),
            ("$target_labels", TargetLabels(targets)),
            ("$target_roles", TargetRoles(targets)));

    public IReadOnlyList<Ad> PendingClassification(int maxAttempts) =>
        Select(pendingClassificationWhere, ("$attempts", maxAttempts));

    public IReadOnlyList<Ad> ExhaustedClassification(int maxAttempts) =>
        Select(exhaustedClassificationWhere, ("$attempts", maxAttempts));

    public void BumpLabelAttempts(IReadOnlyList<string> adIds) => ForEachId(bumpLabelSql, adIds);

    public void SetClassification(string adId, StoredClassification classification, string version) =>
        SqlCommands.Execute(connection, null, setClassificationSql,
            ("$label", classification.Label),
            ("$summary", classification.Summary),
            ("$requires_master", classification.RequiresMaster),
            ("$is_programming_job", classification.IsProgrammingJob),
            ("$display_title", classification.DisplayTitle),
            ("$role_type", classification.RoleType),
            ("$technologies", JsonLists.Write(classification.Technologies)),
            ("$requirements", JsonLists.Write(classification.Requirements)),
            ("$assessment", classification.Assessment),
            ("$version", version),
            ("$at", Now()),
            ("$id", adId));

    public void SetLabel(string adId, string label, string version) =>
        SqlCommands.Execute(connection, null, setLabelSql, ("$label", label), ("$version", version), ("$id", adId));

    public IReadOnlySet<string> KnownIds() =>
        SqlCommands.Query(connection, null, knownIdsSql, reader => reader.GetString(0)).ToHashSet(StringComparer.Ordinal);

    public IReadOnlyList<Ad> NewSince(DateTimeOffset since) =>
        Select(newSinceWhere, ("$since", StoredTime.Format(since)));

    public IReadOnlyList<Ad> Reportable(DateOnly asOf, Func<Ad, bool> isReportable) =>
        KeepReportable(Select(reportableWhere, ("$as_of", StoredTime.Format(asOf))), isReportable);

    public IReadOnlyList<Ad> ReportedSince(DateTimeOffset since) =>
        Select(reportedSinceWhere, ("$since", StoredTime.Format(since)));

    public void MarkReported(IReadOnlyList<string> adIds, DateTimeOffset at) =>
        ForEachId(markReportedSql, adIds, ("$at", StoredTime.Format(at)));

    public IReadOnlyList<Ad> Active() => Select(activeWhere);

    public IReadOnlyList<Ad> ById(IEnumerable<string> adIds) => Select(byIdWhere, ("$ids", JsonLists.Write(adIds)));

    public bool RefreshAssessments(string version) =>
        SqlCommands.Execute(connection, null, refreshSql, ("$version", version)) > 0;

    public long CountOutdated(string version) => SqlCommands.Scalar(connection, null, countOutdatedSql, ("$version", version));

    private static string TargetFiltered(string where, Targets targets) =>
        targets.Limits.HideMasters ? where + noMastersWhere : where;

    private static string TargetLabels(Targets targets) =>
        JsonLists.Write(levelsFromLabel.Where(pair => targets.Limits.Levels.Contains(pair.Value)).Select(pair => pair.Key));

    private static string TargetRoles(Targets targets) => JsonLists.Write([generalSoftware, .. targets.Subfields]);

    private static Ad ReadAd(SqliteDataReader reader) => new() {
        Id = reader.GetString(0),
        Source = reader.GetString(1),
        SourceId = reader.GetString(2),
        Title = reader.GetString(3),
        Company = reader.GetString(4),
        Location = reader.GetString(5),
        Published = reader.OptionalText(6),
        Deadline = reader.OptionalText(7),
        Url = reader.GetString(8),
        Description = reader.OptionalText(9),
        Label = reader.OptionalText(10),
        Summary = reader.OptionalText(11),
        RequiresMaster = reader.GetBoolean(12),
        IsProgrammingJob = reader.IsDBNull(13) ? null : reader.GetBoolean(13),
        DisplayTitle = reader.OptionalText(14),
        RoleType = reader.OptionalText(15),
        Technologies = JsonLists.Read(reader.OptionalText(16)),
        Requirements = JsonLists.Read(reader.OptionalText(17)),
        Assessment = reader.OptionalText(18),
        ApplyUrl = reader.OptionalText(19),
        ApplyStatus = reader.OptionalText(20),
        ApplyCheckedAt = reader.OptionalText(21),
        Republished = reader.GetBoolean(22),
        FirstSeen = reader.OptionalText(23),
        ClosedAt = reader.OptionalText(24),
        FirstClassifiedAt = reader.OptionalText(25),
        AssessmentVersion = reader.OptionalText(26),
    };

    private List<Ad> Select(string where, params ReadOnlySpan<(string Name, object? Value)> values) =>
        Select(null, where, idOrdering, null, values);

    private List<Ad> Select(
        SqliteTransaction? transaction,
        string where,
        string ordering,
        int? limit,
        params ReadOnlySpan<(string Name, object? Value)> values) {
        string limitClause = limit is null ? "" : " LIMIT $limit";
        (string Name, object? Value)[] parameters = limit is int count ? [.. values, ("$limit", count)] : [.. values];
        return SqlCommands.Query(connection, transaction,
            "SELECT " + adColumns + " FROM ads WHERE " + where + " ORDER BY " + ordering + limitClause, ReadAd, parameters);
    }

    private void ForEachId(string sql, IReadOnlyList<string> adIds, params ReadOnlySpan<(string Name, object? Value)> values) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        using SqliteCommand command = SqlCommands.Create(connection, transaction, sql, values);
        SqliteParameter id = command.Parameters.Add("$id", SqliteType.Text);
        foreach (string adId in adIds) {
            id.Value = adId;
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private string Now() => StoredTime.Format(time.GetUtcNow());
}
