from __future__ import annotations

import json
import logging
import re
from dataclasses import dataclass, field, replace
from enum import StrEnum
from typing import TYPE_CHECKING

from jobranger import budget
from jobranger.ads.titles import title_level
from jobranger.events import RunEvent
from jobranger.matching.assessment import AdAssessment, Check
from jobranger.matching.rules import Match, parse_match
from jobranger.matching.text import truncate_at_word
from jobranger.providers.base import (
    ClassifyPrompt,
    ProviderError,
    ProviderErrorKind,
    StopKind,
    Usage,
)

if TYPE_CHECKING:
    from collections.abc import Callable, Sequence
    from datetime import date

    from jobranger.ads.models import Ad
    from jobranger.events import CancelToken, EventSink, RunMode
    from jobranger.providers.base import Provider
    from jobranger.rag.examples import Example
    from jobranger.settings import AppConfig, Limits
    from jobranger.storage.store import Store

logger = logging.getLogger(__name__)

SUMMARY_LIMIT = 700
DISPLAY_TITLE_LIMIT = 80
TECHNOLOGY_TEXT_LIMIT = 60
REQUIREMENT_TEXT_LIMIT = 160
TECHNOLOGY_LIMIT = 5
REQUIREMENT_LIMIT = 3
CHECKS_LIMIT = 5
CHECK_TEXT_LIMIT = 160
REQUIRED_MASTER_SUMMARY_PATTERNS = (
    re.compile(
        r"\b(?:requires?|requiring|must have)\b[^.]{0,100}\bmaster'?s? degree",
        re.I,
    ),
    re.compile(r"\bwith (?:a )?relevant master'?s? degree", re.I),
)
MASTER_ALTERNATIVE_PATTERN = re.compile(
    r"\bbachelor|\bundergraduate|\bequivalent|\bpreferred|\bdesirable|\boptional|"
    r"\bnot (?:required|mandatory|necessary)|\bnot require|n't require",
    re.I,
)
SENTENCE_SPLIT = re.compile(r"[.!?;]")

DATE_HEADING = "Assessment date:"
AD_HEADING = "Advertisement (untrusted JSON data):"


class StopReason(StrEnum):
    budget = "budget"
    auth = "auth"
    provider_down = "provider_down"
    cancelled = "cancelled"
    batch_open = "batch_open"


@dataclass(frozen=True)
class Classification:
    label: str
    summary: str | None
    requires_master: bool
    is_programming_job: bool
    display_title: str | None
    role_type: str | None
    technologies: tuple[str, ...]
    requirements: tuple[str, ...]
    match: Match | None = None
    checks: tuple[Check, ...] = ()


@dataclass(frozen=True)
class ClassifyContext:
    store: Store
    config: AppConfig
    provider: Provider
    system: str
    evidence: frozenset[str]
    education: frozenset[str]
    version: str
    mode: RunMode
    events: EventSink
    cancel: CancelToken
    today: date
    sleep: Callable[[float], None]
    monotonic: Callable[[], float]


@dataclass(frozen=True)
class ClassifyResult:
    assessments: dict[str, Classification] = field(default_factory=dict)
    failed: tuple[str, ...] = ()
    usage: Usage = Usage()
    pending: int = 0
    stopped: StopReason | None = None

    def __add__(self, other: ClassifyResult) -> ClassifyResult:
        return ClassifyResult(
            assessments={**self.assessments, **other.assessments},
            failed=(*self.failed, *other.failed),
            usage=self.usage + other.usage,
            pending=self.pending + other.pending,
            stopped=other.stopped or self.stopped,
        )


def build_prompt(
    ad: Ad,
    system: str,
    date: str,
    examples: Sequence[Example] = (),
) -> ClassifyPrompt:
    user = "\n".join([f"{DATE_HEADING} {date}", AD_HEADING, _ad_json(ad)])
    return ClassifyPrompt(system=system, user=user)


def classify_ads(ads: list[Ad], ctx: ClassifyContext) -> ClassifyResult:
    limits = ctx.config.targets.limits
    assessments: dict[str, Classification] = {}
    failed: list[str] = []
    usage = Usage()
    stopped: StopReason | None = None
    done = 0
    today = ctx.today.isoformat()
    for ad in ads:
        if ctx.cancel.cancelled:
            stopped = StopReason.cancelled
            break
        try:
            result = ctx.provider.classify(
                build_prompt(ad, ctx.system, today), AdAssessment
            )
        except ProviderError as error:
            logger.warning(
                "classify.provider.failed model call stopped kind=%s", error.kind
            )
            stopped = (
                StopReason.auth
                if error.kind == ProviderErrorKind.auth
                else StopReason.provider_down
            )
            break
        done += 1
        usage += result.usage
        budget.record(ctx.store, result.usage, ctx.today)
        classification = (
            to_classification(
                result.parsed, ad, ctx.evidence, ctx.education, limits, ctx.version
            )
            if result.stop == StopKind.done and result.parsed is not None
            else None
        )
        if classification is None:
            failed.append(ad.id)
        else:
            assessments[ad.id] = classification
        ctx.events.emit(RunEvent("model", checked=done, total=len(ads)))
    pending = len(ads) - done if stopped else 0
    return ClassifyResult(assessments, tuple(failed), usage, pending, stopped)


def to_classification(
    assessment: AdAssessment,
    ad: Ad,
    evidence_ids: frozenset[str],
    education_ids: frozenset[str],
    limits: Limits,
    version: str,
) -> Classification | None:
    match = parse_match(
        assessment.match.model_dump(), evidence_ids, education_ids, limits, version
    )
    if match is None:
        return None
    role_fields = _role_fields(assessment)
    if role_fields is None:
        return None
    title, role = role_fields
    summary = assessment.summary[:SUMMARY_LIMIT] or None
    requires_master = assessment.requires_master or _summary_requires_master(summary)
    if requires_master:
        match = replace(match, status="unsuitable")
    result = Classification(
        label=assessment.label,
        summary=summary,
        requires_master=requires_master,
        is_programming_job=assessment.is_programming_job,
        display_title=title,
        role_type=role,
        technologies=_string_tuple(
            assessment.technologies, TECHNOLOGY_LIMIT, TECHNOLOGY_TEXT_LIMIT
        ),
        requirements=_string_tuple(
            assessment.requirements, REQUIREMENT_LIMIT, REQUIREMENT_TEXT_LIMIT
        ),
        match=match,
        checks=_checks(assessment.checks),
    )
    return with_title_level(ad.title, result)


def with_title_level(title: str, result: Classification) -> Classification:
    level = title_level(title)
    if level is None:
        return result
    return replace(result, label=level)


def _ad_json(ad: Ad) -> str:
    return json.dumps(
        {
            "id": ad.id,
            "title": ad.title,
            "employer": ad.company,
            "location": ad.location,
            "description": ad.description or "",
        },
        ensure_ascii=False,
    )


def _role_fields(assessment: AdAssessment) -> tuple[str | None, str | None] | None:
    if not assessment.is_programming_job:
        return None, None
    title = (assessment.display_title or "").strip()
    if not title or assessment.role_type is None:
        return None
    return title[:DISPLAY_TITLE_LIMIT], assessment.role_type


def _checks(checks: Sequence[Check]) -> tuple[Check, ...]:
    return tuple(
        Check(
            text=truncate_at_word(check.text.strip(), CHECK_TEXT_LIMIT),
            state=check.state,
        )
        for check in checks[:CHECKS_LIMIT]
        if check.text.strip()
    )


def _summary_requires_master(summary: str | None) -> bool:
    if summary is None:
        return False
    normalized = summary.replace("’", "'")
    return any(
        not MASTER_ALTERNATIVE_PATTERN.search(sentence)
        and any(
            pattern.search(sentence) for pattern in REQUIRED_MASTER_SUMMARY_PATTERNS
        )
        for sentence in SENTENCE_SPLIT.split(normalized)
    )


def _string_tuple(
    values: Sequence[str], item_limit: int, text_limit: int
) -> tuple[str, ...]:
    items = [
        truncate_at_word(value.strip(), text_limit) for value in values if value.strip()
    ]
    return tuple(items[:item_limit])
