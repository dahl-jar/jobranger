from __future__ import annotations

import json
import sqlite3
from datetime import UTC, datetime
from typing import TYPE_CHECKING

from jobranger.ads.models import Ad, dedup_key
from jobranger.matching.rules import (
    PROFILE_VERSION,
    decode_match,
    encode_match,
    is_reportable,
)

if TYPE_CHECKING:
    from pathlib import Path

    from jobranger.matching.classify import Classification

DESCRIPTION_MAX_ATTEMPTS = 3
SCHEMA = """
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
    first_seen TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_ads_source_source_id ON ads (source, source_id);
CREATE INDEX IF NOT EXISTS ix_ads_dedup_key ON ads (dedup_key);
CREATE TABLE IF NOT EXISTS state (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
"""

AD_COLUMNS = (
    "id, source, source_id, title, company, location, "
    "published, deadline, url, description, label, summary, requires_master, "
    "is_programming_job, display_title, role_type, technologies, requirements, "
    "assessment, apply_url, apply_status, apply_checked_at, republished"
)
AD_COLUMN_COUNT = len(AD_COLUMNS.split(","))
INSERT_EXTRA_COLUMNS = ("assessment_version", "dedup_key", "first_seen")

INSERT_AD_SQL = (
    f"INSERT INTO ads ({AD_COLUMNS}, {', '.join(INSERT_EXTRA_COLUMNS)}) "
    f"VALUES ({', '.join('?' * (AD_COLUMN_COUNT + len(INSERT_EXTRA_COLUMNS)))}) "
    "ON CONFLICT (source, source_id) DO NOTHING"
)


class Store:
    def __init__(self, db_path: Path) -> None:
        self._conn = sqlite3.connect(db_path)
        self._conn.executescript(SCHEMA)
        self._add_missing_columns()

    def upsert_ad(self, ad: Ad) -> bool:
        cursor = self._conn.execute(
            INSERT_AD_SQL,
            (
                ad.id,
                ad.source,
                ad.source_id,
                ad.title,
                ad.company,
                ad.location,
                ad.published,
                ad.deadline,
                ad.url,
                ad.description,
                ad.label,
                ad.summary,
                int(ad.requires_master),
                (
                    int(ad.is_programming_job)
                    if ad.is_programming_job is not None
                    else None
                ),
                ad.display_title,
                ad.role_type,
                json.dumps(ad.technologies, ensure_ascii=False),
                json.dumps(ad.requirements, ensure_ascii=False),
                encode_match(ad.match),
                ad.apply_url,
                ad.apply_status,
                ad.apply_checked_at,
                int(ad.republished),
                ad.match.version if ad.match else None,
                dedup_key(ad.company, ad.title),
                datetime.now(tz=UTC).isoformat(),
            ),
        )
        self._conn.commit()
        return cursor.rowcount == 1

    def pending_description(self) -> list[Ad]:
        return self._select(
            "description IS NULL AND active = 1 "
            "AND (label IS NULL OR label != 'senior') "
            "AND description_attempts < ?",
            (DESCRIPTION_MAX_ATTEMPTS,),
        )

    def bump_description_attempts(self, ad_id: str) -> None:
        self._conn.execute(
            "UPDATE ads SET description_attempts = description_attempts + 1 "
            "WHERE id = ?",
            (ad_id,),
        )
        self._conn.commit()

    def set_description(self, ad_id: str, text: str, apply_url: str | None) -> None:
        self._conn.execute(
            "UPDATE ads SET description = ?, apply_url = COALESCE(?, apply_url) "
            "WHERE id = ?",
            (text, apply_url, ad_id),
        )
        self._conn.commit()

    def set_apply_status(self, ad_id: str, status: str, checked_at: str) -> None:
        self._conn.execute(
            "UPDATE ads SET apply_status = ?, apply_checked_at = ? WHERE id = ?",
            (status, checked_at, ad_id),
        )
        self._conn.commit()

    def pending_apply_url(self, source: str, limit: int) -> list[Ad]:
        return self._select(
            "source = ? AND active = 1 AND is_programming_job = 1 "
            "AND requires_master = 0 AND (label IS NULL OR label != 'senior') "
            "AND description IS NOT NULL AND apply_url IS NULL "
            "AND apply_status IS NULL AND description_attempts < ?",
            (source, DESCRIPTION_MAX_ATTEMPTS),
            ordering="first_seen DESC",
            limit=limit,
        )

    def pending_apply_check(
        self, as_of_date: str, recheck_before: str, limit: int
    ) -> list[Ad]:
        return self._select(
            "active = 1 AND is_programming_job = 1 AND requires_master = 0 "
            "AND (label IS NULL OR label != 'senior') AND apply_url IS NOT NULL "
            "AND (deadline IS NULL OR date(deadline) IS NULL "
            "OR date(deadline) >= date(?)) "
            "AND (apply_checked_at IS NULL OR apply_checked_at < ?) "
            "AND (apply_status IS NULL OR apply_status NOT IN ('expired', 'skipped'))",
            (as_of_date, recheck_before),
            ordering="apply_checked_at IS NOT NULL, first_seen DESC",
            limit=limit,
        )

    def pending_classification(self, max_attempts: int = 3) -> list[Ad]:
        return self._select(
            "description IS NOT NULL AND active = 1 AND label IS NULL "
            "AND label_attempts < ?",
            (max_attempts,),
        )

    def exhausted_classification(self, max_attempts: int = 3) -> list[Ad]:
        return self._select(
            "description IS NOT NULL AND active = 1 AND label IS NULL "
            "AND label_attempts >= ?",
            (max_attempts,),
        )

    def bump_label_attempts(self, ad_ids: list[str]) -> None:
        self._conn.executemany(
            "UPDATE ads SET label_attempts = label_attempts + 1 WHERE id = ?",
            [(ad_id,) for ad_id in ad_ids],
        )
        self._conn.commit()

    def set_classification(self, ad_id: str, classification: Classification) -> None:
        self._conn.execute(
            "UPDATE ads SET label = ?, summary = ?, requires_master = ?, "
            "is_programming_job = ?, display_title = ?, role_type = ?, "
            "technologies = ?, requirements = ?, assessment = ?, "
            "assessment_version = ?, classified_at = ? WHERE id = ?",
            (
                classification.label,
                classification.summary,
                int(classification.requires_master),
                int(classification.is_programming_job),
                classification.display_title,
                classification.role_type,
                json.dumps(classification.technologies, ensure_ascii=False),
                json.dumps(classification.requirements, ensure_ascii=False),
                encode_match(classification.match),
                PROFILE_VERSION,
                datetime.now(tz=UTC).isoformat(),
                ad_id,
            ),
        )
        self._conn.commit()

    def set_label(self, ad_id: str, label: str) -> None:
        self._conn.execute(
            "UPDATE ads SET label = ?, is_programming_job = 0, "
            "assessment = NULL, classified_at = NULL WHERE id = ?",
            (label, ad_id),
        )
        self._conn.commit()

    def known_ids(self) -> set[str]:
        rows = self._conn.execute("SELECT id FROM ads").fetchall()
        return {row[0] for row in rows}

    def new_since(self, iso_ts: str) -> list[Ad]:
        return self._select("first_seen >= ?", (iso_ts,))

    def reportable_ads(self, as_of_date: str, since: str | None = None) -> list[Ad]:
        where = (
            "active = 1 AND is_programming_job = 1 AND requires_master = 0 AND "
            "(deadline IS NULL OR date(deadline) IS NULL OR date(deadline) >= date(?)) "
            "AND (apply_status IS NULL OR apply_status != 'expired')"
        )
        params: list[object] = [as_of_date]
        if since is not None:
            where += (
                " AND (first_seen >= ? OR (classified_at >= ? AND reported_at IS NULL))"
            )
            params.extend((since, since))
        return [ad for ad in self._select(where, tuple(params)) if is_reportable(ad)]

    def reported_ads(self, since: str) -> list[Ad]:
        return self._select("reported_at >= ?", (since,))

    def mark_reported(self, ad_ids: list[str], reported_at: str) -> None:
        self._conn.executemany(
            "UPDATE ads SET reported_at = ? WHERE id = ?",
            [(reported_at, ad_id) for ad_id in ad_ids],
        )
        self._conn.commit()

    def expired_ads(self, as_of_date: str) -> list[Ad]:
        return self._select(
            "active = 1 AND is_programming_job = 1 AND (apply_status = 'expired' "
            "OR (deadline IS NOT NULL AND date(deadline) IS NOT NULL "
            "AND date(deadline) < date(?)))",
            (as_of_date,),
        )

    def active_ads(self) -> list[Ad]:
        return self._select("active = 1")

    def reset_classifications(self) -> None:
        self._reset_assessments("active = 1")

    def refresh_assessments(self) -> bool:
        return (
            self._reset_assessments(
                "active = 1 AND assessment_version IS NOT ?", (PROFILE_VERSION,)
            )
            > 0
        )

    def set_active(self, ad_id: str, active: bool) -> None:
        self._conn.execute(
            "UPDATE ads SET active = ? WHERE id = ?", (int(active), ad_id)
        )
        self._conn.commit()

    def sync_source_active(self, source: str, active_ids: frozenset[str]) -> None:
        self._conn.execute("UPDATE ads SET active = 0 WHERE source = ?", (source,))
        self._conn.executemany(
            "UPDATE ads SET active = 1 WHERE id = ?",
            [(ad_id,) for ad_id in active_ids],
        )
        self._conn.commit()

    def mark_category_unlisted(self, ad_ids: list[str]) -> None:
        self._conn.executemany(
            "UPDATE ads SET category_listed = 0 WHERE id = ?",
            [(ad_id,) for ad_id in ad_ids],
        )
        self._conn.commit()

    def sync_finn_active(
        self,
        category_ids: frozenset[str],
        recent_ids: frozenset[str],
        window_start: str | None,
    ) -> None:
        self._conn.executemany(
            "UPDATE ads SET category_listed = 1 WHERE id = ?",
            [(ad_id,) for ad_id in category_ids],
        )
        self._conn.execute(
            "UPDATE ads SET active = 0 WHERE source = 'finn' AND "
            "(category_listed = 1 OR (? IS NOT NULL AND published >= ?))",
            (window_start, window_start),
        )
        self._conn.executemany(
            "UPDATE ads SET active = 1 WHERE id = ?",
            [(ad_id,) for ad_id in category_ids | recent_ids],
        )
        self._conn.commit()

    def get_state(self, key: str) -> str | None:
        row = self._conn.execute(
            "SELECT value FROM state WHERE key = ?", (key,)
        ).fetchone()
        return row[0] if row else None

    def set_state(self, key: str, value: str) -> None:
        self._conn.execute(
            "INSERT INTO state (key, value) VALUES (?, ?) "
            "ON CONFLICT (key) DO UPDATE SET value = excluded.value",
            (key, value),
        )
        self._conn.commit()

    def _add_missing_columns(self) -> None:
        columns = {
            row[1] for row in self._conn.execute("PRAGMA table_info(ads)").fetchall()
        }
        added = {
            "summary": "ALTER TABLE ads ADD COLUMN summary TEXT",
            "requires_master": (
                "ALTER TABLE ads ADD COLUMN requires_master INTEGER NOT NULL DEFAULT 0"
            ),
            "active": "ALTER TABLE ads ADD COLUMN active INTEGER NOT NULL DEFAULT 1",
            "is_programming_job": (
                "ALTER TABLE ads ADD COLUMN is_programming_job INTEGER"
            ),
            "display_title": "ALTER TABLE ads ADD COLUMN display_title TEXT",
            "role_type": "ALTER TABLE ads ADD COLUMN role_type TEXT",
            "technologies": "ALTER TABLE ads ADD COLUMN technologies TEXT",
            "requirements": "ALTER TABLE ads ADD COLUMN requirements TEXT",
            "assessment": "ALTER TABLE ads ADD COLUMN assessment TEXT",
            "assessment_version": "ALTER TABLE ads ADD COLUMN assessment_version TEXT",
            "classified_at": "ALTER TABLE ads ADD COLUMN classified_at TEXT",
            "apply_url": "ALTER TABLE ads ADD COLUMN apply_url TEXT",
            "apply_status": "ALTER TABLE ads ADD COLUMN apply_status TEXT",
            "apply_checked_at": "ALTER TABLE ads ADD COLUMN apply_checked_at TEXT",
            "republished": (
                "ALTER TABLE ads ADD COLUMN republished INTEGER NOT NULL DEFAULT 0"
            ),
            "category_listed": (
                "ALTER TABLE ads ADD COLUMN category_listed INTEGER NOT NULL DEFAULT 1"
            ),
            "description_attempts": (
                "ALTER TABLE ads ADD COLUMN description_attempts "
                "INTEGER NOT NULL DEFAULT 0"
            ),
        }
        for name, ddl in added.items():
            if name not in columns:
                self._conn.execute(ddl)
        if "reported_at" not in columns:
            self._conn.execute("ALTER TABLE ads ADD COLUMN reported_at TEXT")
            self._conn.execute(
                "UPDATE ads SET reported_at = first_seen WHERE is_programming_job = 1"
            )
        self._conn.commit()

    def _reset_assessments(self, where: str, params: tuple[object, ...] = ()) -> int:
        query = (
            "UPDATE ads SET label = NULL, summary = NULL, requires_master = 0, "
            "is_programming_job = NULL, display_title = NULL, role_type = NULL, "
            "technologies = NULL, requirements = NULL, label_attempts = 0, "
            "assessment = NULL, assessment_version = ?, classified_at = NULL "
            f"WHERE {where}"
        )
        cursor = self._conn.execute(query, (PROFILE_VERSION, *params))
        self._conn.commit()
        return cursor.rowcount

    def _select(
        self,
        where: str,
        params: tuple[object, ...] = (),
        *,
        ordering: str = "id",
        limit: int | None = None,
    ) -> list[Ad]:
        query = f"SELECT {AD_COLUMNS} FROM ads WHERE {where} ORDER BY {ordering}"
        if limit is not None:
            query += " LIMIT ?"
            params = (*params, limit)
        rows = self._conn.execute(query, params).fetchall()
        return [_row_to_ad(row) for row in rows]


def _row_to_ad(row: tuple[object, ...]) -> Ad:
    return Ad(
        id=str(row[0]),
        source=str(row[1]),
        source_id=str(row[2]),
        title=str(row[3]),
        company=str(row[4]),
        location=str(row[5]),
        published=str(row[6]) if row[6] is not None else None,
        deadline=str(row[7]) if row[7] is not None else None,
        url=str(row[8]),
        description=str(row[9]) if row[9] is not None else None,
        label=str(row[10]) if row[10] is not None else None,
        summary=str(row[11]) if row[11] is not None else None,
        requires_master=bool(row[12]),
        is_programming_job=(bool(row[13]) if row[13] is not None else None),
        display_title=str(row[14]) if row[14] is not None else None,
        role_type=str(row[15]) if row[15] is not None else None,
        technologies=_json_tuple(row[16]),
        requirements=_json_tuple(row[17]),
        match=decode_match(row[18]),
        apply_url=str(row[19]) if row[19] is not None else None,
        apply_status=str(row[20]) if row[20] is not None else None,
        apply_checked_at=str(row[21]) if row[21] is not None else None,
        republished=bool(row[22]),
    )


def _json_tuple(value: object) -> tuple[str, ...]:
    if not isinstance(value, str):
        return ()
    try:
        parsed = json.loads(value)
    except json.JSONDecodeError:
        return ()
    if not isinstance(parsed, list):
        return ()
    return tuple(str(item) for item in parsed)
