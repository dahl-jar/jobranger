from __future__ import annotations

import datetime
import logging
import time
from dataclasses import dataclass

import httpx

from jobranger.ads.apply_link import STATUS_EXPIRED
from jobranger.ads.duplicates import group_duplicates
from jobranger.ads.models import Ad
from jobranger.exclusions import is_excluded
from jobranger.matching.classify import Usage
from jobranger.matching.merge import preferred_ad
from jobranger.matching.rules import is_reportable

logger = logging.getLogger(__name__)

REPUBLISHED_AGE_DAYS = 60
STALE_AGE_DAYS = 90
THOUSAND = 1_000
MILLION = 1_000_000


@dataclass(frozen=True)
class ReportContext:
    failed_sources: tuple[str, ...] = ()
    expired_ads: tuple[Ad, ...] = ()
    expired_since: str | None = None
    usage: Usage = Usage()


DISCORD_MESSAGE_LIMIT = 2000
WEBHOOK_PAUSE_SECONDS = 0.5
DISCORD_SUPPRESS_EMBEDS_FLAG = 1 << 2
JOBS_PER_BLOCK = 5
MESSAGE_SEPARATOR = "\n\n---\n\n"
ROLE_LABELS = {
    "frontend": "FRONTEND",
    "backend": "BACKEND",
    "fullstack": "FULL-STACK",
    "mobile": "MOBILE",
    "embedded": "EMBEDDED",
    "data_engineering": "DATA ENGINEERING",
    "ml_ai": "AI / ML",
    "devops_platform": "DEVOPS / PLATFORM",
    "game": "GAME DEVELOPMENT",
    "general_software": "SOFTWARE",
}


def build_summary(
    date: str,
    ads: list[Ad],
    *,
    new_ads_count: int | None = None,
    full_listing: bool = False,
    context: ReportContext | None = None,
) -> str:
    context = context or ReportContext()
    unique, duplicate_count, hidden = _dedup(date, ads, context)
    strong, stretches = _fit_groups(unique, date)
    hidden = [ad for ad in hidden if _is_wanted(ad, date)]
    total = len(strong) + len(stretches)
    heading_suffix = " - full listing" if full_listing else ""
    header = [
        f"**jobranger {date}{heading_suffix}**",
        _count_line(
            total,
            len(strong),
            len(stretches),
            new_ads_count,
            full_listing,
        ),
    ]
    if context.failed_sources:
        header.append(f"**Sources failed:** {', '.join(context.failed_sources)}")
    if duplicate_count:
        duplicate_word = "listing" if duplicate_count == 1 else "listings"
        header.append(f"*{duplicate_count} duplicate source {duplicate_word} merged*")
    header.extend(_expired_lines(hidden))
    header.extend(_usage_lines(context.usage))
    blocks = _listing_blocks(strong, stretches, header, date)
    if not blocks:
        return "\n".join(header)
    return MESSAGE_SEPARATOR.join(blocks)


def _expired_lines(hidden: list[Ad]) -> list[str]:
    if not hidden:
        return []
    listing_word = "listing" if len(hidden) == 1 else "listings"
    lines = [f"*{len(hidden)} expired {listing_word} hidden*"]
    lines.extend(f"{ad.company or 'Not stated'} · {ad.title}" for ad in hidden)
    return lines


def _usage_lines(usage: Usage) -> list[str]:
    if not usage.calls:
        return []
    call_word = "call" if usage.calls == 1 else "calls"
    return [
        f"*Model: {usage.calls} {call_word} · "
        f"{_compact(usage.total_tokens)} tokens "
        f"({_compact(usage.prompt_tokens)} in, {_compact(usage.output_tokens)} out)"
        f" · ${usage.cost_usd:.2f} at API prices*"
    ]


def _compact(count: int) -> str:
    if count < THOUSAND:
        return str(count)
    thousands = round(count / THOUSAND)
    if thousands < THOUSAND:
        return f"{thousands}k"
    return f"{count / MILLION:.1f}M"


def _fit_groups(ads: list[Ad], date: str) -> tuple[list[Ad], list[Ad]]:
    strong: list[Ad] = []
    stretches: list[Ad] = []
    for ad in ads:
        if ad.match is None or not _is_wanted(ad, date):
            continue
        if ad.match.status == "strong":
            strong.append(ad)
        else:
            stretches.append(ad)
    return strong, stretches


def _is_wanted(ad: Ad, date: str) -> bool:
    return is_reportable(ad) and not is_excluded(ad) and not _is_stale(ad, date)


def _count_line(
    total: int,
    strong_count: int,
    stretch_count: int,
    new_ads_count: int | None,
    full_listing: bool,
) -> str:
    listing_word = "job" if total == 1 else "jobs"
    if full_listing or new_ads_count is None:
        return (
            f"**{total} matching {listing_word}** · "
            f"{strong_count} strong match{'es' if strong_count != 1 else ''} · "
            f"{stretch_count} plausible stretch{'es' if stretch_count != 1 else ''}"
        )
    ad_word = "ad" if new_ads_count == 1 else "ads"
    return (
        f"**{new_ads_count} new {ad_word} scanned** · "
        f"**{total} matching {listing_word}**"
    )


def _listing_blocks(
    strong: list[Ad], stretches: list[Ad], header: list[str], date: str
) -> list[str]:
    ordered = [*strong, *stretches]
    blocks: list[str] = []
    first_block = True
    for heading, section_ads in (
        ("Junior / Graduate", [ad for ad in ordered if ad.label == "junior"]),
        ("Open to junior", [ad for ad in ordered if ad.label == "open"]),
        (
            "Experience not specified",
            [ad for ad in ordered if ad.label not in {"junior", "open"}],
        ),
    ):
        for start in range(0, len(section_ads), JOBS_PER_BLOCK):
            page = section_ads[start : start + JOBS_PER_BLOCK]
            end = start + len(page)
            lines = [f"**{heading} · {start + 1}-{end} of {len(section_ads)}**", ""]
            for offset, ad in enumerate(page, start=start + 1):
                lines.extend(_format_ad(offset, ad, date))
                lines.append("")
            if first_block:
                lines = [*header, "", *lines]
                first_block = False
            blocks.append("\n".join(lines).strip())
    return blocks


def send_discord(webhook_url: str, content: str) -> None:
    chunks = split_message(content)
    for chunk in chunks:
        response = httpx.post(
            webhook_url,
            json={
                "content": chunk,
                "flags": DISCORD_SUPPRESS_EMBEDS_FLAG,
                "allowed_mentions": {"parse": []},
            },
        )
        response.raise_for_status()
        time.sleep(WEBHOOK_PAUSE_SECONDS)
    logger.info("report.discord.sent messages=%d", len(chunks))


def _format_ad(index: int, ad: Ad, date: str) -> list[str]:
    title = ad.display_title or ad.title
    role = ROLE_LABELS.get(ad.role_type or "", "SOFTWARE")
    technologies = " · ".join(ad.technologies) or "Stack not stated"
    requirements = " · ".join(ad.requirements) or "No explicit requirements stated"
    company = ad.company or "Not stated"
    location = ad.location or "Not stated"
    lines = [
        f"**{index}. [{title}]({ad.url})**",
        f"`{role}` · {technologies}",
        *_experience_lines(ad),
        f"**Requirements:** {requirements}",
        f"**Company:** {company} · **Location:** {location}",
        *_fit_lines(ad),
    ]
    note = _republished_note(ad, date)
    if note:
        lines.append(note)
    return lines


def _experience_lines(ad: Ad) -> list[str]:
    if ad.match is None or ad.match.required_experience_years is None:
        return []
    years = ad.match.required_experience_years
    unit = "year" if years == 1 else "years"
    return [f"**Experience:** {years:g} {unit} required"]


def _fit_lines(ad: Ad) -> list[str]:
    if ad.match is None:
        return []
    fit = "Strong match" if ad.match.status == "strong" else "Plausible stretch"
    lines = [f"**Fit:** {fit} · {ad.match.reason}"]
    if ad.match.gaps:
        lines.append(f"**Check:** {' · '.join(ad.match.gaps)}")
    return lines


def _is_stale(ad: Ad, date: str) -> bool:
    if not ad.published:
        return False
    return _days_between(date, ad.published[:10]) > STALE_AGE_DAYS


def _republished_note(ad: Ad, date: str) -> str | None:
    first_published = ad.published[:10] if ad.published else None
    if first_published and _days_between(date, first_published) > REPUBLISHED_AGE_DAYS:
        return f"**Note:** Republished listing, first published {first_published}"
    if ad.republished:
        return "**Note:** Republished listing"
    return None


def _days_between(later: str, earlier: str) -> int:
    try:
        return (
            datetime.date.fromisoformat(later) - datetime.date.fromisoformat(earlier)
        ).days
    except ValueError:
        return 0


def _dedup(
    date: str, ads: list[Ad], context: ReportContext
) -> tuple[list[Ad], int, list[Ad]]:
    reportable_ids = {ad.id for ad in ads}
    unique: list[Ad] = []
    hidden: list[Ad] = []
    reportable_groups = 0
    for group in group_duplicates([*ads, *context.expired_ads]):
        preferred = preferred_ad(group)
        reportable = any(ad.id in reportable_ids for ad in group)
        reportable_groups += int(reportable)
        if _is_expired(preferred, date):
            if reportable or any(
                _expired_since(ad, context.expired_since) for ad in group
            ):
                hidden.append(preferred)
        elif reportable:
            unique.append(preferred)
    return unique, len(ads) - reportable_groups, hidden


def _is_expired(ad: Ad, date: str) -> bool:
    if ad.apply_status == STATUS_EXPIRED:
        return True
    return ad.deadline is not None and ad.deadline[:10] < date


def _expired_since(ad: Ad, since: str | None) -> bool:
    if ad.apply_status != STATUS_EXPIRED:
        return False
    return since is None or (ad.apply_checked_at or "") >= since


def split_message(content: str) -> list[str]:
    chunks: list[str] = []
    for block in content.split(MESSAGE_SEPARATOR):
        chunks.extend(_split_block(block))
    return chunks


def _split_block(content: str) -> list[str]:
    chunks: list[str] = []
    current = ""
    for line in content.split("\n"):
        candidate = f"{current}\n{line}" if current else line
        if len(candidate) > DISCORD_MESSAGE_LIMIT and current:
            chunks.append(current)
            current = line
        else:
            current = candidate
    if current:
        chunks.append(current)
    return chunks
