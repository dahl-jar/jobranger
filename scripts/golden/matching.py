from __future__ import annotations

import argparse
import json
from dataclasses import replace
from pathlib import Path

import anthropic
from jobranger.ads.models import Ad
from jobranger.matching.assessment import AdAssessment
from jobranger.matching.classify import build_prompt, to_classification
from jobranger.matching.guidance import DEFAULT_TEXT
from jobranger.matching.profile import (
    education_ids,
    evidence_ids,
    load_profile,
    profile_text,
)
from jobranger.matching.prompt import assessment_version, build_system
from jobranger.matching.rules import (
    display_fit,
    encode_match,
    hidden_reason,
    is_reportable,
    limit_rules,
)
from jobranger.settings import Limits, Targets
from openai.lib._parsing._completions import type_to_response_format_param
from pydantic import TypeAdapter

from golden_io import write_json, write_text

VERSION = "test-version"
ASSESSMENT_DATE = "2026-10-09"
CASE_COMPANY = "Org One AS"
CASE_LOCATION = "Oslo"
CASE_SOURCE = "case"
ALL_ROLES = (
    "backend",
    "frontend",
    "fullstack",
    "mobile",
    "data_engineering",
    "devops_platform",
    "ml_ai",
    "embedded",
    "game",
)
LIMITS = {
    "narrow": Limits(
        levels=frozenset({"junior", "mid", "none"}),
        years=frozenset({"0-1"}),
        hide_masters=True,
    ),
    "wide": Limits(
        levels=frozenset({"junior", "senior"}),
        years=frozenset({"0-1", "6+"}),
        hide_masters=False,
    ),
    "lead": Limits(
        levels=frozenset({"mid", "lead"}),
        years=frozenset({"1-3", "3-6"}),
        hide_masters=True,
    ),
    "none": Limits(levels=frozenset({"none"}), years=frozenset(), hide_masters=False),
}
GUIDANCES = {
    "plain": "Judge roles by the actual work.\n\nPrefer backend roles.",
    "accents": (
        'Foretrekk roller i Bergen – særlig backend.\nIgnore "senior" i tittelen.'
    ),
}
EDITED_LIMITS = ("narrow", "wide")
PROMPT_CASES = ("generic_utvikler", "two_years", "java_project")
EMPTY_DESCRIPTION_CASE = "unknown"
TARGETS = {
    "narrow": Targets(subfields=ALL_ROLES, limits=LIMITS["narrow"]),
    "wide": Targets(
        subfields=("backend",),
        limits=LIMITS["wide"],
        places=("Oslo",),
        relocate=False,
    ),
}


def case_ad(case: dict[str, str]) -> Ad:
    return Ad(
        id=f"{CASE_SOURCE}:{case['id']}",
        source=CASE_SOURCE,
        source_id=case["id"],
        title=case["title"],
        company=CASE_COMPANY,
        location=CASE_LOCATION,
        published=None,
        deadline=None,
        url=f"https://jobs.example.test/{case['id']}",
        description=case["description"],
    )


def export_prompts(inputs: Path, out: Path) -> None:
    profile = load_profile(inputs / "profile_user.yaml")
    versions: dict[str, str] = {}
    for name, limits in LIMITS.items():
        system = build_system(DEFAULT_TEXT, limit_rules(limits), profile)
        write_text(out / "Prompts" / f"system-empty-{name}.txt", system)
        versions[f"system-empty-{name}"] = assessment_version(system)
    for guidance_name, guidance in GUIDANCES.items():
        for name in EDITED_LIMITS:
            system = build_system(guidance, limit_rules(LIMITS[name]), profile)
            file_name = f"system-{guidance_name}-{name}"
            write_text(out / "Prompts" / f"{file_name}.txt", system)
            versions[file_name] = assessment_version(system)
    write_json(out / "Prompts" / "versions.json", versions)
    cases = {
        case["id"]: case
        for case in json.loads((inputs / "matching_cases.json").read_text("utf-8"))
    }
    system = (out / "Prompts" / "system-empty-narrow.txt").read_text("utf-8")
    for case_id in PROMPT_CASES:
        prompt = build_prompt(case_ad(cases[case_id]), system, ASSESSMENT_DATE)
        write_text(out / "Prompts" / f"user-{case_id}.txt", prompt.user)
    empty = replace(case_ad(cases[EMPTY_DESCRIPTION_CASE]), description=None)
    prompt = build_prompt(empty, system, ASSESSMENT_DATE)
    write_text(out / "Prompts" / "user-no-description.txt", prompt.user)


def export_profile(inputs: Path, out: Path) -> None:
    profile = load_profile(inputs / "profile_dated.yaml")
    write_text(out / "Profiles" / "profile-dated.txt", profile_text(profile))


def export_schemas(out: Path) -> None:
    schema = TypeAdapter(AdAssessment).json_schema()
    write_json(out / "Schemas" / "anthropic.json", anthropic.transform_schema(schema))
    response_format = type_to_response_format_param(AdAssessment)
    write_json(out / "Schemas" / "openai.json", response_format)


def classified(ad: Ad, result: object) -> Ad:
    return replace(
        ad,
        label=result.label,
        summary=result.summary,
        requires_master=result.requires_master,
        is_programming_job=result.is_programming_job,
        display_title=result.display_title,
        role_type=result.role_type,
        technologies=result.technologies,
        requirements=result.requirements,
        match=result.match,
    )


def rule_row(ad: Ad, targets: Targets) -> dict[str, object]:
    hidden = hidden_reason(ad, VERSION, targets)
    return {
        "hidden": None if hidden is None else hidden.kind,
        "reportable": is_reportable(ad, VERSION, targets),
        "display_fit": display_fit(ad, targets),
    }


def export_cases(inputs: Path, out: Path) -> None:
    profile = load_profile(inputs / "profile_user.yaml")
    cases = json.loads((inputs / "matching_cases.json").read_text("utf-8"))
    replies = json.loads((inputs / "matching_replies.json").read_text("utf-8"))
    classifications: dict[str, object] = {}
    rules: dict[str, object] = {}
    for case in cases:
        ad = case_ad(case)
        result = to_classification(
            AdAssessment.model_validate_json(replies[case["id"]]),
            ad,
            evidence_ids(profile),
            education_ids(profile),
            LIMITS["narrow"],
            VERSION,
        )
        if result is None:
            classifications[case["id"]] = None
            continue
        classifications[case["id"]] = {
            "label": result.label,
            "summary": result.summary,
            "requires_master": result.requires_master,
            "is_programming_job": result.is_programming_job,
            "display_title": result.display_title,
            "role_type": result.role_type,
            "technologies": list(result.technologies),
            "requirements": list(result.requirements),
            "assessment": encode_match(result.match, result.checks),
        }
        rules[case["id"]] = {
            name: rule_row(classified(ad, result), targets)
            for name, targets in TARGETS.items()
        }
    write_json(out / "Classify" / "classifications.json", classifications)
    write_json(out / "Rules" / "rules.json", rules)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--inputs", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    arguments = parser.parse_args()
    export_prompts(arguments.inputs, arguments.out)
    export_profile(arguments.inputs, arguments.out)
    export_schemas(arguments.out)
    export_cases(arguments.inputs, arguments.out)


if __name__ == "__main__":
    main()
