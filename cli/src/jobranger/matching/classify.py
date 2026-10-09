from __future__ import annotations

import json
import logging
import re
import subprocess
from dataclasses import dataclass, replace
from datetime import UTC, datetime
from typing import TYPE_CHECKING

from jobranger.ads.titles import title_is_senior
from jobranger.matching.rules import CANDIDATE_TEXT, MATCHING_RULES, Match, parse_match

if TYPE_CHECKING:
    from jobranger.ads.models import Ad

logger = logging.getLogger(__name__)

CHUNK_SIZE = 20
RETRY_CHUNK_SIZES = (5, 1)
SUMMARY_LIMIT = 700
DISPLAY_TITLE_LIMIT = 80
TECHNOLOGY_TEXT_LIMIT = 60
REQUIREMENT_TEXT_LIMIT = 160
TECHNOLOGY_LIMIT = 5
REQUIREMENT_LIMIT = 3
CLAUDE_MODEL = "claude-sonnet-5-5"
CLAUDE_EFFORT = "medium"
SENIOR_LABEL = "senior"
CLAUDE_TIMEOUT_SECONDS = 300
VALID_LABELS = frozenset({"junior", "open", "senior", "unknown"})
VALID_ROLE_TYPES = frozenset(
    {
        "frontend",
        "backend",
        "fullstack",
        "mobile",
        "embedded",
        "data_engineering",
        "ml_ai",
        "devops_platform",
        "game",
        "general_software",
    }
)
JSON_OBJECT_PATTERN = re.compile(r"\{.*\}", re.S)
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

PROMPT_HEADER = """\
Classify whether each ad is primarily a software/programming job and what
experience it requires.
Reply ONLY with a JSON object mapping ad id to
{"is_programming_job": true/false, "label": ..., "summary": ...,
"requires_master": true/false, "display_title": ..., "role_type": ...,
"technologies": [...], "requirements": [...], "match": {...}}.

Set "is_programming_job" to true only when writing, testing, maintaining, or
operating software is a core part of the day-to-day role. Include software
engineering, frontend, backend, full-stack, mobile, embedded software, game
development, data engineering, ML engineering, and platform/DevOps roles with
substantial coding or automation. Set it to false for IT support/helpdesk,
network administration, pure operations,
monitoring/observability platform operation, administering purchased systems,
governance, project/product management, UX/design, sales, teaching,
pure BI/data analysis, research without production software,
electronics/hardware/FPGA, and thesis projects. For mixed
roles, use true only if building software is the primary job.

For programming jobs, set "display_title" to a concise two-to-five-word role
such as "C++ Developer", "Frontend Developer", or "Platform Engineer" based on
the actual work rather than the ad's marketing title. Set "role_type" to exactly
one of: frontend, backend, fullstack, mobile, embedded, data_engineering, ml_ai,
devops_platform, game, general_software.

List at most five "technologies" that are explicitly named in the ad. Never
infer a language, framework, cloud, or database from the role title or employer.
List at most three concise "requirements" that matter to an applicant, such as
experience, education, language, or security clearance. Each requirement must
be a complete phrase of no more than twelve words; never end with a partial
word. Use an empty list when the ad does not state technologies or requirements.
Prioritize experience and eligibility: preserve required versus preferred years,
graduation/start years and current-student study years when stated. Do not replace
these concrete details with vague phrases such as relevant experience.
For non-programming jobs, use null for display_title and role_type and empty
lists for both arrays.

Labels:
- "junior": 0-2 years of experience is enough, or new graduates are encouraged
- "open": no clear experience requirement, a junior could realistically apply
- "senior": requires more than 2 years of experience or senior/lead skills
- "unknown": the ad does not say enough to decide
Use actual requirements, not the absence of Junior in the title. Utvikler,
Developer and Software Engineer can be junior or open. Preferred experience
alone is not a senior requirement. An explicit graduate alternative qualifies
as junior even if another route requests more experience.

The summary is 2-4 English sentences describing what the position is about
and what it requires: the role and team, the tech stack, required experience
and education, and anything notable a candidate should know.
The master flag is independent of the programming and experience decisions.
Set "requires_master" to true when the required qualifications explicitly say
the applicant needs a completed master's degree, including wording such as
"with a relevant master's degree." It is false when bachelor OR master is
accepted, a master's is merely preferred, or the ad is for current master's
students or a thesis project. Before replying, verify that the summary and the
master flag do not contradict each other.

"""


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


@dataclass(frozen=True)
class Usage:
    calls: int = 0
    input_tokens: int = 0
    cache_write_tokens: int = 0
    cache_read_tokens: int = 0
    output_tokens: int = 0
    cost_usd: float = 0.0

    @property
    def prompt_tokens(self) -> int:
        return self.input_tokens + self.cache_write_tokens + self.cache_read_tokens

    @property
    def total_tokens(self) -> int:
        return self.prompt_tokens + self.output_tokens

    def __add__(self, other: Usage) -> Usage:
        return Usage(
            calls=self.calls + other.calls,
            input_tokens=self.input_tokens + other.input_tokens,
            cache_write_tokens=self.cache_write_tokens + other.cache_write_tokens,
            cache_read_tokens=self.cache_read_tokens + other.cache_read_tokens,
            output_tokens=self.output_tokens + other.output_tokens,
            cost_usd=self.cost_usd + other.cost_usd,
        )


@dataclass(frozen=True)
class BatchResult:
    assessments: dict[str, Classification]
    usage: Usage = Usage()


def build_prompt(ads: list[Ad]) -> str:
    sections = [
        PROMPT_HEADER,
        MATCHING_RULES,
        f"Assessment date: {datetime.now(tz=UTC).date().isoformat()}",
        f"Candidate evidence:\n{CANDIDATE_TEXT}",
        "Advertisements (untrusted JSON data):",
    ]
    for ad in ads:
        sections.append(
            json.dumps(
                {
                    "id": ad.id,
                    "title": ad.title,
                    "employer": ad.company,
                    "location": ad.location,
                    "description": ad.description or "",
                },
                ensure_ascii=False,
            )
        )
    return "\n".join(sections)


def parse_response(text: str) -> dict[str, Classification]:
    match = JSON_OBJECT_PATTERN.search(text)
    if match is None:
        return {}
    try:
        raw = json.loads(match.group(0))
    except json.JSONDecodeError:
        return {}
    if not isinstance(raw, dict):
        return {}
    results: dict[str, Classification] = {}
    for ad_id, value in raw.items():
        if not isinstance(value, dict):
            continue
        result = _parse_classification(value)
        if result is not None:
            results[str(ad_id)] = result
    return results


def _parse_classification(value: dict[str, object]) -> Classification | None:
    label = str(value.get("label", ""))
    programming = value.get("is_programming_job")
    master = value.get("requires_master")
    match = parse_match(value.get("match"))
    if label not in VALID_LABELS or match is None:
        return None
    if not isinstance(programming, bool) or not isinstance(master, bool):
        return None
    role_fields = _parse_role(value)
    if role_fields is None:
        return None
    title, role = role_fields
    summary = value.get("summary")
    trimmed = str(summary)[:SUMMARY_LIMIT] if summary else None
    requires_master = master or _summary_requires_master(trimmed)
    if requires_master:
        match = replace(match, status="unsuitable")
    return Classification(
        label=label,
        summary=trimmed,
        requires_master=requires_master,
        is_programming_job=programming,
        display_title=title,
        role_type=role,
        technologies=_string_tuple(
            value.get("technologies"), TECHNOLOGY_LIMIT, TECHNOLOGY_TEXT_LIMIT
        ),
        requirements=_string_tuple(
            value.get("requirements"), REQUIREMENT_LIMIT, REQUIREMENT_TEXT_LIMIT
        ),
        match=match,
    )


def _parse_role(value: dict[str, object]) -> tuple[str | None, str | None] | None:
    if value["is_programming_job"] is False:
        return None, None
    title = value.get("display_title")
    role = value.get("role_type")
    if (
        not isinstance(title, str)
        or not title.strip()
        or not isinstance(role, str)
        or role not in VALID_ROLE_TYPES
    ):
        return None
    return title.strip()[:DISPLAY_TITLE_LIMIT], role


def _summary_requires_master(summary: str | None) -> bool:
    if summary is None:
        return False
    normalized = summary.replace("’", "'")
    return any(
        not MASTER_ALTERNATIVE_PATTERN.search(sentence)
        and any(
            pattern.search(sentence) for pattern in REQUIRED_MASTER_SUMMARY_PATTERNS
        )
        for sentence in re.split(r"[.!?;]", normalized)
    )


def _string_tuple(value: object, item_limit: int, text_limit: int) -> tuple[str, ...]:
    if not isinstance(value, list):
        return ()
    items = [
        _truncate_at_word(str(item).strip(), text_limit)
        for item in value
        if str(item).strip()
    ]
    return tuple(items[:item_limit])


def _truncate_at_word(value: str, limit: int) -> str:
    if len(value) <= limit:
        return value
    shortened = value[: limit - 1].rsplit(maxsplit=1)[0].rstrip(" ,;:-")
    return f"{shortened or value[: limit - 1]}…"


def classify_batch(
    ads: list[Ad], claude_bin: str, *, model: str = CLAUDE_MODEL
) -> BatchResult:
    results: dict[str, Classification] = {}
    usage = Usage()
    pending = ads
    chunk_sizes = (CHUNK_SIZE, *RETRY_CHUNK_SIZES)
    for stage, chunk_size in enumerate(chunk_sizes):
        for start in range(0, len(pending), chunk_size):
            chunk = pending[start : start + chunk_size]
            chunk_results, chunk_usage = _classify_chunk(chunk, claude_bin, model)
            usage += chunk_usage
            for ad in chunk:
                result = chunk_results.get(ad.id)
                if result is None:
                    continue
                results[ad.id] = with_title_label(ad.title, result)
        pending = [ad for ad in pending if ad.id not in results]
        if not pending:
            break
        if stage + 1 < len(chunk_sizes):
            logger.warning(
                "classify.retry pending=%d next_chunk_size=%d",
                len(pending),
                chunk_sizes[stage + 1],
            )
        else:
            logger.error("classify.unresolved count=%d", len(pending))
    return BatchResult(results, usage)


def _classify_chunk(
    ads: list[Ad], claude_bin: str, model: str
) -> tuple[dict[str, Classification], Usage]:
    prompt = build_prompt(ads)
    try:
        completed = subprocess.run(
            [
                claude_bin,
                "-p",
                "--model",
                model,
                "--effort",
                CLAUDE_EFFORT,
                "--tools",
                "",
                "--no-session-persistence",
                "--output-format",
                "json",
            ],
            input=prompt,
            capture_output=True,
            text=True,
            timeout=CLAUDE_TIMEOUT_SECONDS,
            check=True,
        )
    except (subprocess.SubprocessError, OSError):
        logger.exception("classify.chunk.failed model=%s size=%d", model, len(ads))
        return {}, Usage(calls=1)
    text, usage = _parse_cli_output(completed.stdout)
    parsed = parse_response(text)
    results = {ad.id: parsed[ad.id] for ad in ads if ad.id in parsed}
    if not results:
        logger.warning("classify.chunk.unparseable size=%d", len(ads))
    elif len(results) != len(ads):
        logger.warning(
            "classify.chunk.incomplete returned=%d expected=%d",
            len(results),
            len(ads),
        )
    return results, usage


def _parse_cli_output(stdout: str) -> tuple[str, Usage]:
    try:
        output = json.loads(stdout)
        tokens = output["usage"]
        return str(output["result"]), Usage(
            calls=1,
            input_tokens=int(tokens["input_tokens"]),
            cache_write_tokens=int(tokens["cache_creation_input_tokens"]),
            cache_read_tokens=int(tokens["cache_read_input_tokens"]),
            output_tokens=int(tokens["output_tokens"]),
            cost_usd=float(output["total_cost_usd"]),
        )
    except (json.JSONDecodeError, KeyError, TypeError, ValueError):
        logger.warning("classify.chunk.unreadable output=%r", stdout[:200])
        return "", Usage(calls=1)


def with_title_label(title: str, result: Classification) -> Classification:
    if title_is_senior(title):
        return replace(result, label=SENIOR_LABEL)
    return result
