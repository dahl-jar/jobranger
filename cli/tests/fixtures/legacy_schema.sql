
CREATE TABLE IF NOT EXISTS ads (
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
    active INTEGER NOT NULL DEFAULT 1,
    is_programming_job INTEGER,
    display_title TEXT,
    role_type TEXT,
    technologies TEXT,
    requirements TEXT,
    dedup_key TEXT NOT NULL,
    first_seen TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_ads_source_source_id ON ads (source, source_id);
CREATE INDEX IF NOT EXISTS ix_ads_dedup_key ON ads (dedup_key);
CREATE TABLE IF NOT EXISTS state (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
