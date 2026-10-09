from __future__ import annotations

from dataclasses import replace
from typing import TYPE_CHECKING

from jobranger.ads.apply_link import STATUS_EXPIRED
from jobranger.ads.duplicates import is_direct_source
from jobranger.matching.rules import has_current_match

if TYPE_CHECKING:
    from jobranger.ads.models import Ad


def preferred_ad(group: list[Ad]) -> Ad:
    selected = max(
        group,
        key=lambda ad: (
            is_direct_source(ad),
            len(ad.description or ""),
        ),
    )
    classified = max(
        group,
        key=lambda ad: (
            has_current_match(ad),
            _has_blocked_match(ad),
            len(ad.technologies) + len(ad.requirements),
            bool(ad.display_title),
            len(ad.description or ""),
        ),
    )
    content = max(group, key=lambda ad: len(ad.description or ""))
    label = "senior" if any(ad.label == "senior" for ad in group) else classified.label
    merged = replace(
        selected,
        description=content.description,
        label=label,
        summary=classified.summary,
        requires_master=any(ad.requires_master for ad in group),
        is_programming_job=classified.is_programming_job,
        display_title=classified.display_title,
        role_type=classified.role_type,
        technologies=classified.technologies,
        requirements=classified.requirements,
        match=classified.match,
    )
    return _merge_source_fields(merged, group)


def _merge_source_fields(ad: Ad, group: list[Ad]) -> Ad:
    expired = any(member.apply_status == STATUS_EXPIRED for member in group)
    return replace(
        ad,
        deadline=_earliest([member.deadline for member in group]),
        published=_earliest([member.published for member in group]),
        republished=any(member.republished for member in group),
        apply_status=STATUS_EXPIRED if expired else ad.apply_status,
        apply_url=ad.apply_url or _first([member.apply_url for member in group]),
    )


def _earliest(values: list[str | None]) -> str | None:
    return min((value for value in values if value), default=None)


def _first(values: list[str | None]) -> str | None:
    return next((value for value in values if value), None)


def _has_blocked_match(ad: Ad) -> bool:
    match = ad.match
    return match is not None and (
        match.status == "unsuitable" or bool(match.blockers) or match.is_low_code
    )
