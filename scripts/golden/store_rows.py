from __future__ import annotations

import argparse
import dataclasses
import json
import sqlite3
from pathlib import Path

from jobranger.ads.models import Ad
from jobranger.matching.assessment import Check
from jobranger.matching.classify import Classification
from jobranger.matching.rules import Match
from jobranger.providers.base import Usage
from jobranger.storage.store import (
    ApplicationChange,
    NewApplication,
    RunSummary,
    Store,
)

VERSION = "golden-version"
OUTDATED_VERSION = "old-version"
AT = "2026-10-09T10:00:00+00:00"
LATER = "2026-10-10T10:00:00.250000+00:00"
CLOSED_AS_OF = "2026-10-20"
USAGE_SINCE = "2026-01-01"
SPEND_START = "2026-10-01"
SPEND_END = "2026-11-01"
RUN_LIMIT = 10
HUGE_COST = 1e308
NEGATIVE_COST = -1.0
TEXT_COUNT = "x"


def make_match(version: str, blockers: tuple[str, ...] = ()) -> Match:
    return Match(
        status="strong",
        reason="Java project shows backend work.",
        evidence=("java_project",),
        gaps=(),
        blockers=blockers,
        is_low_code=False,
        required_experience_years=2.0,
        version=version,
    )


def base_ad(index: int, **overrides: object) -> Ad:
    fields: dict[str, object] = {
        "id": f"finn:{index}",
        "source": "finn",
        "source_id": str(index),
        "title": "Utvikler",
        "company": "Org One AS",
        "location": "Bergen",
        "published": "2026-09-24T00:00:00+00:00",
        "deadline": None,
        "url": f"https://jobs.example.test/ad/{index}",
    }
    fields.update(overrides)
    return Ad(**fields)


def seed_ads(store: Store) -> None:
    store.upsert_ad(
        base_ad(
            1,
            title="Backend  Utvikler",
            company="Org  One ASA",
            deadline="2026-12-01",
            description="Bygg tjenester i Java.",
            label="junior",
            summary="Backend work.",
            requires_master=True,
            is_programming_job=True,
            display_title="Backend Developer",
            role_type="backend",
            technologies=("Java", "Søk", 'C"#'),
            requirements=("Bachelor", "line\nbreak", "back\\slash"),
            match=make_match(VERSION),
            apply_url="https://jobs.example.test/apply/1",
            apply_status="active",
            apply_checked_at=AT,
            republished=False,
        )
    )
    store.upsert_ad(base_ad(2, source="nav", id="nav:2", published=None, location=""))
    store.upsert_ad(
        base_ad(3, deadline="2026-10-01", match=make_match(OUTDATED_VERSION))
    )
    store.upsert_ad(base_ad(4, description="Gone soon."))
    store.upsert_ad(
        base_ad(5, apply_url="https://jobs.example.test/apply/5", republished=True)
    )
    store.upsert_ad(base_ad(6, title="Senior Utvikler"))
    store.upsert_ad(base_ad(7, company="Org Two", title="Ærlig utvikler"))


def classify(store: Store) -> None:
    classification = Classification(
        label="mid",
        summary="Platform work.",
        requires_master=False,
        is_programming_job=True,
        display_title="Platform Developer",
        role_type="devops_platform",
        technologies=("Kubernetes",),
        requirements=(),
        match=make_match(VERSION, blockers=("Needs five years",)),
        checks=(Check(text="Kubernetes", state="met"),),
    )
    store.set_classification("finn:7", classification, VERSION)
    store.set_label("finn:6", "senior")
    store.bump_label_attempts(["finn:6", "finn:6"])
    store.bump_description_attempts("finn:4")


def change_states(store: Store) -> None:
    store.close_past_deadline("2026-10-05")
    store.set_active("finn:4", False)
    store.set_apply_status("finn:5", "expired", LATER)
    store.mark_category_unlisted(["finn:7"])
    store.mark_reported(["finn:1"], AT)
    store.mark_ad("finn:1", "show_anyway", AT)
    store.mark_ad("nav:2", "skipped", AT)
    store.mark_ad("finn:3", "applied", AT, cv_name="cv.pdf")
    store.mark_ad("finn:4", "not_for_me", LATER)
    store.set_state("nav_position", "https://feed.example.test/page/2")
    store.set_state("finn_since", AT)


def seed_applications(store: Store) -> None:
    manual = store.create_application(
        NewApplication(
            company="Org Three AS",
            role="Developer",
            place="Oslo",
            deadline="2026-11-01",
            note="",
            applied_at=AT,
            cv_name=None,
            ad_id=None,
        )
    )
    store.update_application(
        manual, ApplicationChange(status="interview", note="Called back"), LATER
    )
    store.add_application_event(manual, LATER, "Phone screen")
    removed = store.create_application(
        NewApplication(
            company="Org Four",
            role="Tester",
            place="",
            deadline=None,
            note="Old",
            applied_at=LATER,
            cv_name="cv-two.docx",
            ad_id=None,
        )
    )
    store.remove_application(removed, LATER)


def seed_runs_and_usage(store: Store, db_path: Path) -> None:
    store.start_run(AT)
    finished = store.start_run(LATER)
    store.finish_run(
        finished,
        RunSummary(
            finished_at="2026-10-10T10:05:00+00:00",
            status="completed",
            ads_read=7,
            new_ads=5,
            matches=3,
            hidden=2,
            failed_sources=("nav", "linkedin"),
        ),
    )
    store.start_run("2026-10-11T10:00:00+00:00")
    store.add_usage(
        "2026-10-09",
        Usage(
            calls=2,
            input_tokens=100,
            cache_write_tokens=10,
            output_tokens=50,
            cost_usd=0.25,
        ),
        ads_checked=2,
    )
    store.add_usage("2026-10-10", Usage(), ads_read=7)
    with sqlite3.connect(db_path) as conn:
        conn.execute(
            "INSERT INTO usage_days (day, calls, input_tokens, output_tokens, "
            "cost_usd, ads_read, ads_checked) VALUES (?, ?, ?, ?, ?, ?, ?)",
            ("2026-10-11", -2, TEXT_COUNT, 3, NEGATIVE_COST, -1, TEXT_COUNT),
        )
        conn.execute(
            "INSERT INTO usage_days (day, cost_usd) VALUES (?, ?)",
            ("2026-10-12", HUGE_COST),
        )


def ad_row(ad: Ad, raw: sqlite3.Row) -> dict[str, object]:
    fields = dataclasses.asdict(ad)
    del fields["match"]
    fields["technologies"] = list(ad.technologies)
    fields["requirements"] = list(ad.requirements)
    fields["assessment"] = raw["assessment"]
    fields["assessment_version"] = raw["assessment_version"]
    fields["dedup_key"] = raw["dedup_key"]
    return fields


def dump(store: Store, db_path: Path) -> dict[str, object]:
    with sqlite3.connect(db_path) as conn:
        conn.row_factory = sqlite3.Row
        raw = {
            row["id"]: row
            for row in conn.execute(
                "SELECT id, assessment, assessment_version, dedup_key FROM ads"
            )
        }
    ads = store.ads_by_id(sorted(store.known_ids()))
    return {
        "ads": [ad_row(ad, raw[ad.id]) for ad in ads],
        "actions": {
            ad_id: sorted(actions)
            for ad_id, actions in sorted(store.ad_actions().items())
        },
        "closed_as_of": CLOSED_AS_OF,
        "closed": [
            {
                "id": item.ad.id,
                "closed_on": item.closed_on,
                "why": item.why,
                "you": item.you,
            }
            for item in store.closed_ads(CLOSED_AS_OF)
        ],
        "applications": [
            dataclasses.asdict(application)
            for application in store.applications(include_removed=True)
        ],
        "runs": [dataclasses.asdict(run) for run in store.runs(RUN_LIMIT)],
        "usage_since": USAGE_SINCE,
        "usage_days": [
            dataclasses.asdict(day) for day in store.usage_days(USAGE_SINCE)
        ],
        "spend": {
            "start": SPEND_START,
            "end": SPEND_END,
            "usd": store.spend_between(SPEND_START, SPEND_END),
        },
        "cursors": store.cursors(),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Write the Python-made store golden")
    parser.add_argument("--db", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    args.db.unlink(missing_ok=True)
    store = Store(args.db)
    seed_ads(store)
    classify(store)
    change_states(store)
    seed_applications(store)
    seed_runs_and_usage(store, args.db)
    rows = dump(store, args.db)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(
        json.dumps(rows, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
