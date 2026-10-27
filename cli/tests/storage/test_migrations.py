from __future__ import annotations

import sqlite3
from typing import TYPE_CHECKING

import pytest

from jobranger.storage.migrations import (
    MIGRATIONS,
    Migration,
    MigrationError,
    SchemaTooNewError,
    current_version,
    migrate,
)

if TYPE_CHECKING:
    from pathlib import Path

FIRST = Migration(1, ("CREATE TABLE state (key TEXT PRIMARY KEY, value TEXT)",))
SECOND = Migration(2, ("CREATE TABLE extra (id INTEGER PRIMARY KEY)",))


def _tables(conn: sqlite3.Connection) -> set[str]:
    rows = conn.execute("SELECT name FROM sqlite_master WHERE type = 'table'")
    return {row[0] for row in rows}


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
    migrate(conn, (FIRST, SECOND))

    with pytest.raises(SchemaTooNewError, match=r"schema 2 is newer .*\(1\)") as raised:
        migrate(conn, (FIRST,))

    assert (raised.value.found, raised.value.supported) == (2, 1)


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
