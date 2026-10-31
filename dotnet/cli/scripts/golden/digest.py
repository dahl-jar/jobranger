from __future__ import annotations

import argparse
import json
import os
import time
from datetime import date, datetime
from pathlib import Path
from urllib.parse import quote, urlunsplit

from jobranger.ads.models import Ad
from jobranger.matching.classify import StopReason
from jobranger.notify import email
from jobranger.notify.digest import DigestData
from jobranger.notify.email import DigestLinks, render_digest

TIME_ZONE = "Europe/Oslo"
CASES = "cases.json"
SUBJECTS = "subjects.json"
GROUPS = ("apply_soon", "new_today", "carried")
HIDDEN_LABELS = {
    "senior": ("senior", "senior"),
    "masters": ("master's", "master's"),
    "role": ("other role", "other roles"),
    "place": ("other place", "other places"),
    "other": ("other reason", "other reasons"),
}
UNKNOWN_YEARS_LABEL = "need more yrs"


def card_links(ad: Ad, site: str | None) -> tuple[email._Link, ...]:
    found: list[email._Link] = []
    parts = email._web_parts(ad.url)
    if parts:
        found.append(
            email._Link(f"Open the ad {email.NORTH_EAST_ARROW}", urlunsplit(parts))
        )
    if not site:
        return tuple(found)
    ad_link = f"{site}/app/ad?id={quote(email._replace_surrogates(ad.id), safe='')}"
    if len(ad_link) <= email.URL_CHAR_LIMIT:
        found.append(email._Link("Mark applied", f"{ad_link}&action=applied"))
        found.append(
            email._Link("Not for me", f"{ad_link}&action=not_for_me", muted=True)
        )
    return tuple(found)


def hidden_label(kind: str, count: int, max_years: float | None) -> str:
    if kind == "years":
        if max_years is None:
            return UNKNOWN_YEARS_LABEL
        return f"need {round(max_years)}+ yrs"
    singular, plural = HIDDEN_LABELS[kind]
    return singular if count == 1 else plural


def hidden_line(hidden: dict[str, int], max_years: float | None) -> str:
    counts = sorted(
        (
            (count, hidden_label(kind, count, max_years))
            for kind, count in hidden.items()
            if count > 0
        ),
        key=lambda part: -part[0],
    )
    if not counts:
        return ""
    total = sum(count for count, _ in counts)
    parts = email.MIDDLE_DOT.join(f"{count} {label}" for count, label in counts)
    return f"{total} hidden: {parts}"


def expand(cards: list[dict[str, object]]) -> list[dict[str, object]]:
    expanded: list[dict[str, object]] = []
    for card in cards:
        copies = card.get("copies")
        if copies is None:
            expanded.append(card)
            continue
        expanded.extend(
            {**card, "id": f"{card['id']}-{index}"} for index in range(1, copies + 1)
        )
    return expanded


def to_ad(card: dict[str, object]) -> Ad:
    return Ad(
        id=card["id"],
        source=card["source"],
        source_id=card["id"],
        title=card["title"],
        company=card["company"],
        location=card["location"],
        published=None,
        deadline=card["deadline"],
        url=card["url"],
        summary=card["summary"],
        display_title=card["display_title"],
        role_type=card["role_label"],
    )


def digest_data(case: dict[str, object], groups: dict[str, list[Ad]]) -> DigestData:
    stopped = case.get("model_stopped")
    return DigestData(
        day=date.fromisoformat(case["day"]),
        ran_at=datetime.fromisoformat(case["ran_at"]),
        apply_soon=groups["apply_soon"],
        new_today=groups["new_today"],
        carried=groups["carried"],
        hidden=case.get("hidden", {}),
        ads_read=0,
        new_ads=case.get("new_ads", 0),
        sources_read=case.get("sources_read", []),
        employer_pages=case.get("employer_pages", 0),
        failed_sources=case.get("failed_sources", []),
        closed_since_last=case.get("closed_since_last", 0),
        budget_paused=case.get("budget_paused", False),
        pending=case.get("pending", 0),
        max_years=case.get("max_years"),
        model_stopped=None if stopped is None else StopReason(stopped),
    )


def render_case(
    base: dict[str, object], case: dict[str, object]
) -> email.RenderedDigest:
    fits: dict[str, str] = {}
    groups: dict[str, list[Ad]] = {}
    for group in GROUPS:
        cards = expand([{**base, **card} for card in case.get(group, [])])
        fits.update((card["id"], card["fit"]) for card in cards)
        groups[group] = [to_ad(card) for card in cards]
    email.display_fit = lambda ad, targets: fits[ad.id]
    return render_digest(
        digest_data(case, groups), DigestLinks(case.get("site_url")), None
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Write the Python digest goldens")
    parser.add_argument("--inputs", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    os.environ["TZ"] = TIME_ZONE
    time.tzset()
    email._card_links = card_links
    email.hidden_line = hidden_line
    email.role_label = lambda role: role
    cases = json.loads((args.inputs / CASES).read_text(encoding="utf-8"))
    args.out.mkdir(parents=True, exist_ok=True)
    subjects: dict[str, dict[str, object]] = {}
    for name, case in cases["cases"].items():
        rendered = render_case(cases["card"], case)
        (args.out / f"{name}.html").write_bytes(rendered.html.encode())
        (args.out / f"{name}.txt").write_bytes(rendered.text.encode())
        subjects[name] = {"subject": rendered.subject, "ad_ids": list(rendered.ad_ids)}
    (args.out / SUBJECTS).write_text(
        json.dumps(subjects, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
