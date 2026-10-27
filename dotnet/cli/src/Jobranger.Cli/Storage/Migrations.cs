using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Storage;

internal static class Migrations {
    private const int noSchema = 0;
    private const string hasStateSql = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'state'";

    private static readonly Migration baseline = new(1, [
        """
        CREATE TABLE ads (
            id TEXT PRIMARY KEY,
            source TEXT NOT NULL,
            source_id TEXT NOT NULL,
            title TEXT NOT NULL,
            company TEXT NOT NULL,
            location TEXT NOT NULL DEFAULT '',
            published TEXT,
            deadline TEXT,
            url TEXT NOT NULL,
            description TEXT,
            label TEXT,
            summary TEXT,
            requires_master INTEGER NOT NULL DEFAULT 0,
            label_attempts INTEGER NOT NULL DEFAULT 0,
            description_attempts INTEGER NOT NULL DEFAULT 0,
            active INTEGER NOT NULL DEFAULT 1,
            is_programming_job INTEGER,
            display_title TEXT,
            role_type TEXT,
            technologies TEXT,
            requirements TEXT,
            assessment TEXT,
            assessment_version TEXT,
            classified_at TEXT,
            apply_url TEXT,
            apply_status TEXT,
            apply_checked_at TEXT,
            republished INTEGER NOT NULL DEFAULT 0,
            reported_at TEXT,
            category_listed INTEGER NOT NULL DEFAULT 1,
            dedup_key TEXT NOT NULL,
            first_seen TEXT NOT NULL,
            closed_at TEXT,
            closed_reason TEXT CHECK (closed_reason IN ('deadline', 'removed', 'filled'))
        )
        """,
        "CREATE UNIQUE INDEX uq_ads_source_source_id ON ads (source, source_id)",
        "CREATE INDEX ix_ads_dedup_key ON ads (dedup_key)",
        "CREATE INDEX ix_ads_closed_at ON ads (closed_at) WHERE closed_at IS NOT NULL",
        """
        CREATE TABLE state (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE ad_actions (
            ad_id TEXT NOT NULL REFERENCES ads (id) ON DELETE CASCADE,
            action TEXT NOT NULL
                CHECK (action IN ('applied', 'skipped', 'not_for_me', 'show_anyway')),
            at TEXT NOT NULL,
            PRIMARY KEY (ad_id, action)
        ) WITHOUT ROWID
        """,
        """
        CREATE TABLE applications (
            id INTEGER PRIMARY KEY,
            ad_id TEXT REFERENCES ads (id) ON DELETE SET NULL,
            company TEXT NOT NULL CHECK (length(trim(company)) > 0),
            role TEXT NOT NULL CHECK (length(trim(role)) > 0),
            place TEXT NOT NULL DEFAULT '',
            deadline TEXT,
            status TEXT NOT NULL DEFAULT 'waiting'
                CHECK (status IN ('waiting', 'interview', 'offer', 'accepted', 'rejected')),
            note TEXT NOT NULL DEFAULT '',
            applied_at TEXT NOT NULL,
            cv_name TEXT,
            removed_at TEXT
        )
        """,
        "CREATE UNIQUE INDEX uq_applications_ad_id ON applications (ad_id) WHERE ad_id IS NOT NULL",
        """
        CREATE TABLE application_events (
            id INTEGER PRIMARY KEY,
            application_id INTEGER NOT NULL
                REFERENCES applications (id) ON DELETE CASCADE,
            at TEXT NOT NULL,
            text TEXT NOT NULL CHECK (length(text) <= 500)
        )
        """,
        "CREATE INDEX ix_application_events_application ON application_events (application_id, at)",
        """
        CREATE TABLE runs (
            id INTEGER PRIMARY KEY,
            started_at TEXT NOT NULL,
            finished_at TEXT,
            status TEXT NOT NULL
                CHECK (status IN ('running', 'completed', 'cancelled', 'failed')),
            ads_read INTEGER NOT NULL DEFAULT 0,
            new_ads INTEGER NOT NULL DEFAULT 0,
            matches INTEGER NOT NULL DEFAULT 0,
            hidden INTEGER NOT NULL DEFAULT 0,
            failed_sources TEXT NOT NULL DEFAULT '[]'
        )
        """,
        "CREATE INDEX ix_runs_started_at ON runs (started_at)",
        """
        CREATE TABLE usage_days (
            day TEXT PRIMARY KEY,
            calls INTEGER NOT NULL DEFAULT 0,
            input_tokens INTEGER NOT NULL DEFAULT 0,
            output_tokens INTEGER NOT NULL DEFAULT 0,
            cost_usd REAL NOT NULL DEFAULT 0,
            ads_read INTEGER NOT NULL DEFAULT 0,
            ads_checked INTEGER NOT NULL DEFAULT 0
        ) WITHOUT ROWID
        """,
    ]);

    private static readonly Migration firstClassification = new(2, [
        "ALTER TABLE ads ADD COLUMN first_classified_at TEXT",
        "UPDATE ads SET first_classified_at = COALESCE(classified_at, first_seen) WHERE label IS NOT NULL OR classified_at IS NOT NULL",
    ]);

    private static readonly Migration decisions = new(3, [
        """
        CREATE TABLE decisions (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            ad_id TEXT NOT NULL UNIQUE,
            dedup_key TEXT NOT NULL,
            action TEXT NOT NULL CHECK (action IN ('applied', 'not_for_me', 'show_anyway')),
            decided_at TEXT NOT NULL,
            title TEXT NOT NULL,
            company TEXT NOT NULL,
            summary TEXT NOT NULL,
            requirements TEXT NOT NULL,
            embed_text TEXT NOT NULL,
            embedded_model TEXT
        )
        """,
        """
        CREATE TABLE classification_examples (
            ad_id TEXT NOT NULL REFERENCES ads (id) ON DELETE CASCADE,
            decision_id INTEGER NOT NULL REFERENCES decisions (id) ON DELETE CASCADE,
            PRIMARY KEY (ad_id, decision_id)
        ) WITHOUT ROWID
        """,
        """
        CREATE TABLE decision_vectors (
            decision_id INTEGER PRIMARY KEY REFERENCES decisions (id) ON DELETE CASCADE,
            embedding BLOB NOT NULL
        )
        """,
    ]);

    private static readonly Migration devices = new(4, [
        """
        CREATE TABLE devices (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL,
            public_key BLOB NOT NULL,
            paired_at TEXT NOT NULL,
            last_seen TEXT
        )
        """,
    ]);

    public static readonly IReadOnlyList<Migration> All = [baseline, firstClassification, decisions, devices];

    public static int CurrentVersion(SqliteConnection connection) => CurrentVersion(connection, null);

    public static void Migrate(SqliteConnection connection, IReadOnlyList<Migration> migrations) {
        int version = CurrentVersion(connection);
        int newest = migrations.Max(migration => migration.Version);
        if (version > newest) {
            throw new SchemaTooNewException(version, newest);
        }
        foreach (Migration migration in migrations.OrderBy(migration => migration.Version)) {
            if (migration.Version <= version) {
                continue;
            }
            Apply(connection, migration);
        }
    }

    private static int CurrentVersion(SqliteConnection connection, SqliteTransaction? transaction) {
        using SqliteCommand hasState = SqlCommands.Create(connection, transaction, hasStateSql);
        if (hasState.ExecuteScalar() is null) {
            return noSchema;
        }
        string? version = StateStore.Get(connection, transaction, StateStore.SchemaVersionKey);
        return version is null ? noSchema : int.Parse(version, CultureInfo.InvariantCulture);
    }

    private static void Apply(SqliteConnection connection, Migration migration) {
        using SqliteTransaction transaction = Database.BeginImmediate(connection);
        if (CurrentVersion(connection, transaction) >= migration.Version) {
            transaction.Commit();
            return;
        }
        try {
            foreach (string statement in migration.Statements) {
                SqlCommands.Execute(connection, transaction, statement);
            }
            StateStore.Set(connection, transaction, StateStore.SchemaVersionKey,
                migration.Version.ToString(CultureInfo.InvariantCulture));
            transaction.Commit();
        } catch (SqliteException error) {
            transaction.Rollback();
            throw new MigrationException(migration.Version, error);
        }
    }
}
