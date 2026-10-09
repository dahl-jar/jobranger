from __future__ import annotations

import json
import os
import sqlite3
from dataclasses import dataclass
from datetime import UTC, datetime
from typing import TYPE_CHECKING, Literal, cast

from jobranger.ads.apply_link import STATUS_EXPIRED
from jobranger.ads.models import Ad, dedup_key
from jobranger.catalogue import GENERAL_SOFTWARE
from jobranger.matching.rules import (
    LEVELS_FROM_LABEL,
    decode_match,
    encode_match,
    is_reportable,
)
from jobranger.providers.base import Usage
from jobranger.storage.migrations import SCHEMA_VERSION_KEY, migrate

if TYPE_CHECKING:
    from pathlib import Path

    from jobranger.matching.classify import Classification
    from jobranger.settings import Targets

type Action = Literal["applied", "skipped", "not_for_me", "show_anyway"]
type ApplicationStatus = Literal[
    "waiting", "interview", "offer", "accepted", "rejected"
]
type ClosedWhy = Literal["deadline", "removed", "filled"]
type StoredRunStatus = Literal["running", "completed", "cancelled", "failed"]

DESCRIPTION_MAX_ATTEMPTS = 3
CLOSED_KEEP_DAYS = 30
BUSY_TIMEOUT_MS = 5000
DB_FILE_MODE = 0o600
DB_DIR_MODE = 0o700
EXCLUSIVE_ACTIONS: tuple[Action, ...] = ("applied", "skipped", "not_for_me")
APPLIED_EVENT = "Applied"
STATUS_EVENT = "Status: {status}"
CLOSE_COLUMNS = (
    "active = 0, closed_at = COALESCE(closed_at, ?), "
    "closed_reason = COALESCE(closed_reason, 'removed')"
)
REOPEN_COLUMNS = "active = 1, closed_at = NULL, closed_reason = NULL"

SHARED_COLUMNS = (
    "id, source, source_id, title, company, location, "
    "published, deadline, url, description, label, summary, requires_master, "
    "is_programming_job, display_title, role_type, technologies, requirements, "
    "assessment, apply_url, apply_status, apply_checked_at, republished"
)
AD_COLUMNS = f"{SHARED_COLUMNS}, first_seen, closed_at"
INSERT_COLUMNS = f"{SHARED_COLUMNS}, assessment_version, dedup_key, first_seen"
INSERT_AD_SQL = (
    f"INSERT INTO ads ({INSERT_COLUMNS}) "
    f"VALUES ({', '.join('?' * len(INSERT_COLUMNS.split(',')))}) "
    "ON CONFLICT (source, source_id) DO NOTHING"
)
APPLICATION_COLUMNS = (
    "id, ad_id, company, role, place, deadline, status, note, applied_at, "
    "cv_name, removed_at"
)


@dataclass(frozen=True)
class ClosedAd:
    ad: Ad
    closed_on: str
    why: ClosedWhy
    you: Action | None


@dataclass(frozen=True)
class NewApplication:
    company: str
    role: str
    place: str
    deadline: str | None
    note: str
    applied_at: str
    cv_name: str | None
    ad_id: str | None


@dataclass(frozen=True)
class ApplicationChange:
    status: ApplicationStatus | None = None
    note: str | None = None


@dataclass(frozen=True)
class ApplicationEvent:
    at: str
    text: str


@dataclass(frozen=True)
class Application:
    id: int
    ad_id: str | None
    company: str
    role: str
    place: str
    deadline: str | None
    status: ApplicationStatus
    note: str
    applied_at: str
    cv_name: str | None
    removed_at: str | None
    events: tuple[ApplicationEvent, ...]


@dataclass(frozen=True)
class RunSummary:
    finished_at: str
    status: StoredRunStatus
    ads_read: int
    new_ads: int
    matches: int
    hidden: int
    failed_sources: tuple[str, ...]


@dataclass(frozen=True)
class RunRow:
    id: int
    started_at: str
    finished_at: str | None
    status: StoredRunStatus
    ads_read: int
    new_ads: int
    matches: int
    hidden: int
    failed_sources: tuple[str, ...]


@dataclass(frozen=True)
class UsageDay:
    day: str
    calls: int
    input_tokens: int
    output_tokens: int
    cost_usd: float
    ads_read: int
    ads_checked: int


class Store:
    def __init__(self, db_path: Path) -> None:
        _create_private(db_path)
        self._conn = sqlite3.connect(db_path)
        self._conn.execute("PRAGMA foreign_keys = ON")
        self._conn.execute(f"PRAGMA busy_timeout = {BUSY_TIMEOUT_MS}")
        migrate(self._conn)

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
                _optional_int(ad.is_programming_job),
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
                _now(),
            ),
        )
        self._conn.commit()
        return cursor.rowcount == 1

    def pending_description(self, skip_labels: frozenset[str]) -> list[Ad]:
        return self._select(
            "description IS NULL AND active = 1 "
            "AND (label IS NULL OR label NOT IN (SELECT value FROM json_each(?))) "
            "AND description_attempts < ?",
            (_json_list(skip_labels), DESCRIPTION_MAX_ATTEMPTS),
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
        if status == STATUS_EXPIRED:
            self._conn.execute(
                "UPDATE ads SET apply_status = ?, apply_checked_at = ?, "
                "closed_at = COALESCE(closed_at, ?), "
                "closed_reason = COALESCE(closed_reason, 'filled') WHERE id = ?",
                (status, checked_at, checked_at, ad_id),
            )
        else:
            self._conn.execute(
                "UPDATE ads SET apply_status = ?, apply_checked_at = ? WHERE id = ?",
                (status, checked_at, ad_id),
            )
        self._conn.commit()

    def pending_apply_url(self, source: str, limit: int, targets: Targets) -> list[Ad]:
        where, params = _target_filter(targets)
        return self._select(
            f"source = ? AND active = 1 AND {where} "
            "AND description IS NOT NULL AND apply_url IS NULL "
            "AND apply_status IS NULL AND description_attempts < ?",
            (source, *params, DESCRIPTION_MAX_ATTEMPTS),
            ordering="first_seen DESC",
            limit=limit,
        )

    def pending_apply_check(
        self, as_of_date: str, recheck_before: str, limit: int, targets: Targets
    ) -> list[Ad]:
        where, params = _target_filter(targets)
        return self._select(
            f"active = 1 AND {where} AND apply_url IS NOT NULL "
            "AND (deadline IS NULL OR date(deadline) IS NULL "
            "OR date(deadline) >= date(?)) "
            "AND (apply_checked_at IS NULL OR apply_checked_at < ?) "
            "AND (apply_status IS NULL OR apply_status NOT IN ('expired', 'skipped'))",
            (*params, as_of_date, recheck_before),
            ordering="apply_checked_at IS NOT NULL, first_seen DESC",
            limit=limit,
        )

    def pending_classification(self, max_attempts: int) -> list[Ad]:
        return self._select(
            "description IS NOT NULL AND active = 1 AND label IS NULL "
            "AND label_attempts < ?",
            (max_attempts,),
        )

    def exhausted_classification(self, max_attempts: int) -> list[Ad]:
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

    def set_classification(
        self, ad_id: str, classification: Classification, version: str
    ) -> None:
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
                encode_match(classification.match, classification.checks),
                version,
                _now(),
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

    def known_ids(self) -> frozenset[str]:
        rows = self._conn.execute("SELECT id FROM ads").fetchall()
        return frozenset(row[0] for row in rows)

    def cursors(self) -> dict[str, str]:
        rows = self._conn.execute(
            "SELECT key, value FROM state WHERE key != ?", (SCHEMA_VERSION_KEY,)
        ).fetchall()
        return {str(key): str(value) for key, value in rows}

    def new_since(self, iso_ts: str) -> list[Ad]:
        return self._select("first_seen >= ?", (iso_ts,))

    def reportable_ads(
        self, as_of_date: str, since: str | None, version: str, targets: Targets
    ) -> list[Ad]:
        where = (
            "active = 1 AND "
            "(deadline IS NULL OR date(deadline) IS NULL OR date(deadline) >= date(?)) "
            "AND (apply_status IS NULL OR apply_status != 'expired')"
        )
        params: list[object] = [as_of_date]
        if since is not None:
            where += (
                " AND (first_seen >= ? OR (classified_at >= ? AND reported_at IS NULL))"
            )
            params.extend((since, since))
        shown = {
            ad_id
            for ad_id, actions in self.ad_actions().items()
            if "show_anyway" in actions
        }
        return [
            ad
            for ad in self._select(where, tuple(params))
            if ad.id in shown or is_reportable(ad, version, targets)
        ]

    def reported_ads(self, since: str) -> list[Ad]:
        return self._select("reported_at >= ?", (since,))

    def mark_reported(self, ad_ids: list[str], reported_at: str) -> None:
        self._conn.executemany(
            "UPDATE ads SET reported_at = ? WHERE id = ?",
            [(reported_at, ad_id) for ad_id in ad_ids],
        )
        self._conn.commit()

    def active_ads(self) -> list[Ad]:
        return self._select("active = 1")

    def ads_by_id(self, ad_ids: list[str]) -> list[Ad]:
        return self._select(
            "id IN (SELECT value FROM json_each(?))", (_json_list(ad_ids),)
        )

    def refresh_assessments(self, version: str) -> bool:
        cursor = self._conn.execute(
            "UPDATE ads SET label = NULL, summary = NULL, requires_master = 0, "
            "is_programming_job = NULL, display_title = NULL, role_type = NULL, "
            "technologies = NULL, requirements = NULL, label_attempts = 0, "
            "assessment = NULL, assessment_version = ?, classified_at = NULL "
            "WHERE active = 1 AND assessment_version IS NOT ?",
            (version, version),
        )
        self._conn.commit()
        return cursor.rowcount > 0

    def set_active(self, ad_id: str, active: bool) -> None:
        if active:
            self._conn.execute(
                f"UPDATE ads SET {REOPEN_COLUMNS} WHERE id = ? AND active = 0",
                (ad_id,),
            )
        else:
            self._conn.execute(
                f"UPDATE ads SET {CLOSE_COLUMNS} WHERE id = ? AND active = 1",
                (_now(), ad_id),
            )
        self._conn.commit()

    def sync_source_active(self, source: str, active_ids: frozenset[str]) -> None:
        listed = _json_list(active_ids)
        self._conn.execute(
            f"UPDATE ads SET {CLOSE_COLUMNS} WHERE source = ? AND active = 1 "
            "AND id NOT IN (SELECT value FROM json_each(?))",
            (_now(), source, listed),
        )
        self._reopen(listed)
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
        listed = _json_list(category_ids | recent_ids)
        self._conn.execute(
            f"UPDATE ads SET {CLOSE_COLUMNS} WHERE source = 'finn' AND active = 1 "
            "AND (category_listed = 1 OR (? IS NOT NULL AND published >= ?)) "
            "AND id NOT IN (SELECT value FROM json_each(?))",
            (_now(), window_start, window_start, listed),
        )
        self._reopen(listed)
        self._conn.commit()

    def close_past_deadline(self, as_of_date: str) -> int:
        cursor = self._conn.execute(
            "UPDATE ads SET closed_at = ?, closed_reason = 'deadline' "
            "WHERE active = 1 AND closed_at IS NULL AND deadline IS NOT NULL "
            "AND date(deadline) IS NOT NULL AND date(deadline) < date(?)",
            (as_of_date, as_of_date),
        )
        self._conn.commit()
        return cursor.rowcount

    def closed_ads(
        self, as_of_date: str, keep_days: int = CLOSED_KEEP_DAYS
    ) -> list[ClosedAd]:
        rows = self._conn.execute(
            "SELECT id, closed_at, closed_reason FROM ads WHERE closed_at IS NOT NULL "
            "AND date(closed_at) >= date(?, ?) ORDER BY closed_at DESC, id",
            (as_of_date, f"-{keep_days} days"),
        ).fetchall()
        ads = {ad.id: ad for ad in self.ads_by_id([str(row[0]) for row in rows])}
        actions = self.ad_actions()
        return [
            ClosedAd(
                ad=ads[str(ad_id)],
                closed_on=str(closed_at)[:10],
                why=cast("ClosedWhy", reason),
                you=_your_action(actions.get(str(ad_id), frozenset())),
            )
            for ad_id, closed_at, reason in rows
        ]

    def closed_since(self, since: str) -> int:
        row = self._conn.execute(
            "SELECT COUNT(*) FROM ads WHERE closed_at >= ?", (since,)
        ).fetchone()
        return int(row[0])

    def mark_ad(
        self, ad_id: str, action: Action, at: str, cv_name: str | None = None
    ) -> None:
        with self._conn:
            if action in EXCLUSIVE_ACTIONS:
                for other in EXCLUSIVE_ACTIONS:
                    if other != action:
                        self._delete_action(ad_id, other, at)
            self._conn.execute(
                "INSERT INTO ad_actions (ad_id, action, at) VALUES (?, ?, ?) "
                "ON CONFLICT (ad_id, action) DO UPDATE SET at = excluded.at",
                (ad_id, action, at),
            )
            if action == "applied":
                self._apply_for(ad_id, at, cv_name)

    def unmark_ad(self, ad_id: str, action: Action, at: str) -> None:
        with self._conn:
            self._delete_action(ad_id, action, at)

    def ad_actions(self) -> dict[str, frozenset[Action]]:
        found: dict[str, set[Action]] = {}
        for ad_id, action in self._conn.execute(
            "SELECT ad_id, action FROM ad_actions"
        ).fetchall():
            found.setdefault(str(ad_id), set()).add(cast("Action", action))
        return {ad_id: frozenset(actions) for ad_id, actions in found.items()}

    def create_application(self, new: NewApplication) -> int:
        with self._conn:
            return self._insert_application(new)

    def update_application(
        self, app_id: int, change: ApplicationChange, at: str
    ) -> None:
        with self._conn:
            if change.status is not None:
                self._conn.execute(
                    "UPDATE applications SET status = ? WHERE id = ?",
                    (change.status, app_id),
                )
                self._add_event(
                    app_id, at, STATUS_EVENT.format(status=change.status.capitalize())
                )
            if change.note is not None:
                self._conn.execute(
                    "UPDATE applications SET note = ? WHERE id = ?",
                    (change.note, app_id),
                )

    def remove_application(self, app_id: int, at: str) -> None:
        with self._conn:
            self._conn.execute(
                "UPDATE applications SET removed_at = ? WHERE id = ?", (at, app_id)
            )

    def restore_application(self, app_id: int) -> None:
        with self._conn:
            self._conn.execute(
                "UPDATE applications SET removed_at = NULL WHERE id = ?", (app_id,)
            )

    def add_application_event(self, app_id: int, at: str, text: str) -> None:
        with self._conn:
            self._add_event(app_id, at, text)

    def applications(self, include_removed: bool = False) -> list[Application]:
        where = "" if include_removed else " WHERE removed_at IS NULL"
        rows = self._conn.execute(
            f"SELECT {APPLICATION_COLUMNS} FROM applications{where} "
            "ORDER BY applied_at DESC, id DESC"
        ).fetchall()
        events: dict[int, list[ApplicationEvent]] = {}
        for app_id, at, text in self._conn.execute(
            "SELECT application_id, at, text FROM application_events ORDER BY at, id"
        ).fetchall():
            events.setdefault(int(app_id), []).append(
                ApplicationEvent(str(at), str(text))
            )
        return [_row_to_application(row, events.get(int(row[0]), [])) for row in rows]

    def start_run(self, started_at: str) -> int:
        with self._conn:
            self._conn.execute(
                "UPDATE runs SET status = 'failed', finished_at = ? "
                "WHERE status = 'running'",
                (started_at,),
            )
            cursor = self._conn.execute(
                "INSERT INTO runs (started_at, status) VALUES (?, 'running')",
                (started_at,),
            )
        return int(cursor.lastrowid or 0)

    def finish_run(self, run_id: int, summary: RunSummary) -> None:
        with self._conn:
            self._conn.execute(
                "UPDATE runs SET finished_at = ?, status = ?, ads_read = ?, "
                "new_ads = ?, matches = ?, hidden = ?, failed_sources = ? "
                "WHERE id = ?",
                (
                    summary.finished_at,
                    summary.status,
                    summary.ads_read,
                    summary.new_ads,
                    summary.matches,
                    summary.hidden,
                    json.dumps(list(summary.failed_sources)),
                    run_id,
                ),
            )

    def last_run(self) -> RunRow | None:
        rows = self.runs(limit=1)
        return rows[0] if rows else None

    def runs(self, limit: int) -> list[RunRow]:
        rows = self._conn.execute(
            "SELECT id, started_at, finished_at, status, ads_read, new_ads, matches, "
            "hidden, failed_sources FROM runs ORDER BY started_at DESC, id DESC "
            "LIMIT ?",
            (limit,),
        ).fetchall()
        return [_row_to_run(row) for row in rows]

    def add_usage(
        self, day: str, usage: Usage, ads_read: int = 0, ads_checked: int = 0
    ) -> None:
        with self._conn:
            self._conn.execute(
                "INSERT INTO usage_days (day, calls, input_tokens, output_tokens, "
                "cost_usd, ads_read, ads_checked) VALUES (?, ?, ?, ?, ?, ?, ?) "
                "ON CONFLICT (day) DO UPDATE SET calls = calls + excluded.calls, "
                "input_tokens = input_tokens + excluded.input_tokens, "
                "output_tokens = output_tokens + excluded.output_tokens, "
                "cost_usd = cost_usd + excluded.cost_usd, "
                "ads_read = ads_read + excluded.ads_read, "
                "ads_checked = ads_checked + excluded.ads_checked",
                (
                    day,
                    usage.calls,
                    usage.prompt_tokens,
                    usage.output_tokens,
                    usage.cost_usd,
                    ads_read,
                    ads_checked,
                ),
            )

    def usage_days(self, since: str) -> list[UsageDay]:
        rows = self._conn.execute(
            "SELECT day, calls, input_tokens, output_tokens, cost_usd, ads_read, "
            "ads_checked FROM usage_days WHERE day >= ? ORDER BY day",
            (since,),
        ).fetchall()
        return [
            UsageDay(
                str(day),
                int(calls),
                int(input_tokens),
                int(output_tokens),
                float(cost),
                int(read),
                int(checked),
            )
            for day, calls, input_tokens, output_tokens, cost, read, checked in rows
        ]

    def set_state(self, key: str, value: str) -> None:
        self._conn.execute(
            "INSERT INTO state (key, value) VALUES (?, ?) "
            "ON CONFLICT (key) DO UPDATE SET value = excluded.value",
            (key, value),
        )
        self._conn.commit()

    def _reopen(self, listed: str) -> None:
        self._conn.execute(
            f"UPDATE ads SET {REOPEN_COLUMNS} WHERE active = 0 "
            "AND id IN (SELECT value FROM json_each(?))",
            (listed,),
        )

    def _delete_action(self, ad_id: str, action: Action, at: str) -> None:
        cursor = self._conn.execute(
            "DELETE FROM ad_actions WHERE ad_id = ? AND action = ?", (ad_id, action)
        )
        if action == "applied" and cursor.rowcount:
            self._conn.execute(
                "UPDATE applications SET removed_at = ? "
                "WHERE ad_id = ? AND removed_at IS NULL",
                (at, ad_id),
            )

    def _apply_for(self, ad_id: str, at: str, cv_name: str | None) -> None:
        existing = self._conn.execute(
            "SELECT id FROM applications WHERE ad_id = ?", (ad_id,)
        ).fetchone()
        if existing is not None:
            self._conn.execute(
                "UPDATE applications SET removed_at = NULL WHERE id = ?",
                (existing[0],),
            )
            return
        ad = self.ads_by_id([ad_id])[0]
        self._insert_application(
            NewApplication(
                company=ad.company,
                role=ad.display_title or ad.title,
                place=ad.location,
                deadline=ad.deadline,
                note="",
                applied_at=at,
                cv_name=cv_name,
                ad_id=ad_id,
            )
        )

    def _insert_application(self, new: NewApplication) -> int:
        cursor = self._conn.execute(
            "INSERT INTO applications (ad_id, company, role, place, deadline, note, "
            "applied_at, cv_name) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
            (
                new.ad_id,
                new.company,
                new.role,
                new.place,
                new.deadline,
                new.note,
                new.applied_at,
                new.cv_name,
            ),
        )
        app_id = int(cursor.lastrowid or 0)
        self._add_event(app_id, new.applied_at, APPLIED_EVENT)
        return app_id

    def _add_event(self, app_id: int, at: str, text: str) -> None:
        self._conn.execute(
            "INSERT INTO application_events (application_id, at, text) "
            "VALUES (?, ?, ?)",
            (app_id, at, text),
        )

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


def _create_private(db_path: Path) -> None:
    db_path.parent.mkdir(mode=DB_DIR_MODE, parents=True, exist_ok=True)
    os.close(os.open(db_path, os.O_RDWR | os.O_CREAT, DB_FILE_MODE))


def _target_filter(targets: Targets) -> tuple[str, tuple[object, ...]]:
    labels = [
        label
        for label, level in LEVELS_FROM_LABEL.items()
        if level in targets.limits.levels
    ]
    roles = [GENERAL_SOFTWARE, *targets.subfields]
    where = (
        "is_programming_job = 1 "
        "AND (label IS NULL OR label IN (SELECT value FROM json_each(?))) "
        "AND role_type IN (SELECT value FROM json_each(?))"
    )
    if targets.limits.hide_masters:
        where += " AND requires_master = 0"
    return where, (_json_list(labels), _json_list(roles))


def _your_action(actions: frozenset[Action]) -> Action | None:
    return next((action for action in EXCLUSIVE_ACTIONS if action in actions), None)


def _json_list(values: frozenset[str] | list[str]) -> str:
    return json.dumps(sorted(values))


def _now() -> str:
    return datetime.now(tz=UTC).isoformat()


def _optional_int(value: bool | None) -> int | None:
    return int(value) if value is not None else None


def _optional_str(value: object) -> str | None:
    return str(value) if value is not None else None


def _row_to_ad(row: tuple[object, ...]) -> Ad:
    return Ad(
        id=str(row[0]),
        source=str(row[1]),
        source_id=str(row[2]),
        title=str(row[3]),
        company=str(row[4]),
        location=str(row[5]),
        published=_optional_str(row[6]),
        deadline=_optional_str(row[7]),
        url=str(row[8]),
        description=_optional_str(row[9]),
        label=_optional_str(row[10]),
        summary=_optional_str(row[11]),
        requires_master=bool(row[12]),
        is_programming_job=(bool(row[13]) if row[13] is not None else None),
        display_title=_optional_str(row[14]),
        role_type=_optional_str(row[15]),
        technologies=_json_tuple(row[16]),
        requirements=_json_tuple(row[17]),
        match=decode_match(row[18]),
        apply_url=_optional_str(row[19]),
        apply_status=_optional_str(row[20]),
        apply_checked_at=_optional_str(row[21]),
        republished=bool(row[22]),
        first_seen=_optional_str(row[23]),
        closed_at=_optional_str(row[24]),
    )


def _row_to_application(
    row: tuple[object, ...], events: list[ApplicationEvent]
) -> Application:
    return Application(
        id=int(cast("int", row[0])),
        ad_id=_optional_str(row[1]),
        company=str(row[2]),
        role=str(row[3]),
        place=str(row[4]),
        deadline=_optional_str(row[5]),
        status=cast("ApplicationStatus", row[6]),
        note=str(row[7]),
        applied_at=str(row[8]),
        cv_name=_optional_str(row[9]),
        removed_at=_optional_str(row[10]),
        events=tuple(events),
    )


def _row_to_run(row: tuple[object, ...]) -> RunRow:
    return RunRow(
        id=int(cast("int", row[0])),
        started_at=str(row[1]),
        finished_at=_optional_str(row[2]),
        status=cast("StoredRunStatus", row[3]),
        ads_read=int(cast("int", row[4])),
        new_ads=int(cast("int", row[5])),
        matches=int(cast("int", row[6])),
        hidden=int(cast("int", row[7])),
        failed_sources=_json_tuple(row[8]),
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
