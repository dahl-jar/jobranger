from __future__ import annotations

import json
from dataclasses import replace

import pytest
from factories import FAKE_VERSION, FIXTURES, targets
from record_matching_replies import LIMITS, case_ad

from jobranger.ads.models import Ad
from jobranger.matching.assessment import AdAssessment
from jobranger.matching.classify import Classification, to_classification
from jobranger.matching.profile import Profile, education_ids, evidence_ids
from jobranger.matching.rules import is_reportable

CASES = json.loads((FIXTURES / "matching_cases.json").read_text(encoding="utf-8"))
REPLIES = json.loads((FIXTURES / "matching_replies.json").read_text(encoding="utf-8"))
CLASSIFICATION_FIELDS = ("label", "requires_master", "is_programming_job")
MATCH_FIELDS = ("is_low_code", "required_experience_years")


def _classify(
    profile: Profile, case: dict[str, str], assessment: AdAssessment
) -> Classification | None:
    return to_classification(
        assessment,
        case_ad(case),
        evidence_ids(profile),
        education_ids(profile),
        LIMITS,
        FAKE_VERSION,
    )


def _classified_ad(case: dict[str, str], result: Classification) -> Ad:
    return replace(
        case_ad(case),
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


@pytest.mark.parametrize("case", CASES, ids=[case["id"] for case in CASES])
def test_should_reach_expected_verdict_for_matching_case(
    fake_profile: Profile, case: dict[str, object]
) -> None:
    result = _classify(
        fake_profile, case, AdAssessment.model_validate_json(REPLIES[case["id"]])
    )

    assert result is not None
    assert result.match is not None
    observed = {
        "status": result.match.status,
        **{name: getattr(result, name) for name in CLASSIFICATION_FIELDS},
        **{name: getattr(result.match, name) for name in MATCH_FIELDS},
        "reportable": is_reportable(
            _classified_ad(case, result), FAKE_VERSION, targets()
        ),
        "evidence": case.get("evidence") in result.match.evidence,
        "requirement_text": any(
            str(case.get("requirement_text")) in item for item in result.requirements
        ),
    }
    expected = {
        "status": case["expected"],
        **{
            name: case[name]
            for name in (*CLASSIFICATION_FIELDS, *MATCH_FIELDS)
            if name in case
        },
        **({"reportable": case["reportable"]} if "reportable" in case else {}),
        **({"evidence": True} if "evidence" in case else {}),
        **({"requirement_text": True} if "requirement_text" in case else {}),
    }
    assert {name: observed[name] for name in expected} == expected


def test_should_force_unsuitable_when_reply_flags_master_but_says_strong(
    fake_profile: Profile,
) -> None:
    case = next(case for case in CASES if case["id"] == "injection")
    reply = json.loads(REPLIES["injection"])
    reply["requires_master"] = True
    reply["match"] = {
        **reply["match"],
        "status": "strong",
        "evidence": ["java_project"],
        "gaps": [],
        "blockers": [],
        "is_low_code": False,
    }

    result = _classify(fake_profile, case, AdAssessment.model_validate(reply))

    assert result is not None
    assert result.match is not None
    assert result.match.status == "unsuitable"
