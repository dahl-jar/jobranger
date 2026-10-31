from __future__ import annotations

import sqlite3
from typing import TYPE_CHECKING

import pytest

from jobranger.storage.migrations import (
    MIGRATIONS,
    SCHEMA_VERSION_KEY,
    Migration,
    MigrationError,
    SchemaTooNewError,
    current_version,
    migrate,
)
from jobranger.storage.store import Store

if TYPE_CHECKING:
    from pathlib import Path

FIRST = Migration(1, ("CREATE TABLE state (key TEXT PRIMARY KEY, value TEXT)",))
SECOND = Migration(2, ("CREATE TABLE extra (id INTEGER PRIMARY KEY)",))
DEVICES_VERSION = 4
CSHARP_VERSION = 5
NEWER_VERSION = 6
DEVICE_COLUMNS = [
    ("id", "TEXT", 0, 1),
    ("name", "TEXT", 1, 0),
    ("public_key", "BLOB", 1, 0),
    ("paired_at", "TEXT", 1, 0),
    ("last_seen", "TEXT", 0, 0),
]


def _tables(conn: sqlite3.Connection) -> set[str]:
    rows = conn.execute("SELECT name FROM sqlite_master WHERE type = 'table'")
    return {row[0] for row in rows}


def _set_version(conn: sqlite3.Connection, version: int) -> None:
    conn.execute(
        "UPDATE state SET value = ? WHERE key = ?", (str(version), SCHEMA_VERSION_KEY)
    )
    conn.commit()


def test_should_apply_only_migrations_above_stored_version(tmp_path: Path) -> None:
    conn = sqlite3.connect(tmp_path / "db.sqlite")
    migrate(conn, (FIRST,))

    migrate(conn, (FIRST, SECOND))

    assert "extra" in _tables(conn)


def test_should_record_schema_version_after_migrating(tmp_path: Path) -> None:
    conn = sqlite3.connect(tmp_path / "db.sqlite")

    version = migrate(conn)

    assert version == MIGRATIONS[-1].version
    assert current_version(conn) == MIGRATIONS[-1].version


def test_should_roll_back_failed_migration_completely(tmp_path: Path) -> None:
    conn = sqlite3.connect(tmp_path / "db.sqlite")
    migrate(conn, (FIRST,))
    broken = Migration(
        2, ("CREATE TABLE half (id INTEGER PRIMARY KEY)", "CREATE TABLE (oops")
    )

    with pytest.raises(MigrationError, match="migration 2 failed") as raised:
        migrate(conn, (FIRST, broken))

    assert ("half" in _tables(conn), current_version(conn), raised.value.version) == (
        False,
        1,
        2,
    )


def test_should_refuse_database_newer_than_code(tmp_path: Path) -> None:
    conn = sqlite3.connect(tmp_path / "db.sqlite")
    migrate(conn)
    _set_version(conn, NEWER_VERSION)

    with pytest.raises(SchemaTooNewError, match=r"schema 6 is newer .*\(5\)") as raised:
        migrate(conn)

    assert (raised.value.found, raised.value.supported) == (
        NEWER_VERSION,
        CSHARP_VERSION,
    )


def test_should_read_ads_when_csharp_migrated_database(tmp_path: Path) -> None:
    path = tmp_path / "db.sqlite"
    conn = sqlite3.connect(path)
    migrate(conn, MIGRATIONS[:2])
    conn.execute(
        "INSERT INTO ads (id, source, source_id, title, company, url, dedup_key, "
        "first_seen) VALUES ('a', 'finn', '1', 't', 'c', 'u', 'k', 'seen-a')"
    )
    _set_version(conn, CSHARP_VERSION)
    conn.close()

    ads = Store(path).active_ads()

    assert [ad.id for ad in ads] == ["a"]


def test_should_create_devices_table_like_csharp(tmp_path: Path) -> None:
    conn = sqlite3.connect(tmp_path / "db.sqlite")

    migrate(conn)

    rows = conn.execute("PRAGMA table_info(devices)").fetchall()
    columns = [(name, kind, notnull, key) for _, name, kind, notnull, _, key in rows]
    assert columns == DEVICE_COLUMNS


def test_should_stay_at_version_zero_without_migrations(tmp_path: Path) -> None:
    conn = sqlite3.connect(tmp_path / "db.sqlite")

    assert migrate(conn, ()) == 0


def test_should_backfill_first_classification_for_classified_ads(
    tmp_path: Path,
) -> None:
    conn = sqlite3.connect(tmp_path / "db.sqlite")
    migrate(conn, MIGRATIONS[:1])
    conn.execute(
        "INSERT INTO ads (id, source, source_id, title, company, url, dedup_key, "
        "first_seen, label, classified_at) VALUES "
        "('a', 'finn', '1', 't', 'c', 'u', 'k', 'seen-a', 'junior', 'checked-a'), "
        "('b', 'finn', '2', 't', 'c', 'u', 'k', 'seen-b', 'junior', NULL), "
        "('c', 'finn', '3', 't', 'c', 'u', 'k', 'seen-c', NULL, NULL)"
    )
    conn.commit()

    migrate(conn)

    rows = conn.execute("SELECT id, first_classified_at FROM ads ORDER BY id")
    assert rows.fetchall() == [("a", "checked-a"), ("b", "seen-b"), ("c", None)]


def test_should_reset_label_attempts_when_upgrading_from_version_four(
    tmp_path: Path,
) -> None:
    conn = sqlite3.connect(tmp_path / "db.sqlite")
    migrate(conn, MIGRATIONS[:DEVICES_VERSION])
    conn.execute(
        "INSERT INTO ads (id, source, source_id, title, company, url, dedup_key, "
        "first_seen, label, label_attempts) VALUES "
        "('a', 'finn', '1', 't', 'c', 'u', 'k', 'seen-a', 'junior', 2), "
        "('b', 'finn', '2', 't', 'c', 'u', 'k', 'seen-b', 'error', 2), "
        "('c', 'finn', '3', 't', 'c', 'u', 'k', 'seen-c', NULL, 2)"
    )
    conn.commit()

    migrate(conn)

    rows = conn.execute("SELECT id, label_attempts FROM ads ORDER BY id")
    assert rows.fetchall() == [("a", 0), ("b", 2), ("c", 2)]
