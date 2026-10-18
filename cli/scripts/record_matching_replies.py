from __future__ import annotations

import json
import logging
import subprocess
import sys
from pathlib import Path

from jobranger.ads.models import Ad
from jobranger.matching.assessment import AdAssessment
from jobranger.matching.classify import build_prompt
from jobranger.matching.guidance import DEFAULT_TEXT
from jobranger.matching.profile import load_profile
from jobranger.matching.prompt import build_system
from jobranger.matching.rules import limit_rules
from jobranger.settings import Limits

logger = logging.getLogger(__name__)

FIXTURES = Path(__file__).resolve().parent.parent / "tests" / "fixtures"
CASES_PATH = FIXTURES / "matching_cases.json"
PROFILE_PATH = FIXTURES / "profile_user.yaml"
REPLIES_PATH = FIXTURES / "matching_replies.json"
ASSESSMENT_DATE = "2026-10-09"
CASE_COMPANY = "Org One AS"
CASE_LOCATION = "Oslo"
CASE_SOURCE = "case"
SCHEMA_FLAG = "--json-schema"
CALL_TIMEOUT_SECONDS = 300
LIMITS = Limits(
    levels=frozenset({"junior", "mid", "none"}),
    years=frozenset({"0-1"}),
    hide_masters=True,
)


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


def reply_text(output: str) -> str:
    payload = json.loads(output)
    structured = payload.get("structured_output")
    if structured is not None:
        return json.dumps(structured, ensure_ascii=False)
    return str(payload["result"])


def record(command: list[str]) -> dict[str, str]:
    system = build_system(DEFAULT_TEXT, limit_rules(LIMITS), load_profile(PROFILE_PATH))
    schema = json.dumps(AdAssessment.model_json_schema())
    replies: dict[str, str] = {}
    for case in json.loads(CASES_PATH.read_text(encoding="utf-8")):
        prompt = build_prompt(case_ad(case), system, ASSESSMENT_DATE)
        completed = subprocess.run(
            [*command, SCHEMA_FLAG, schema],
            input=f"{prompt.system}\n{prompt.user}",
            capture_output=True,
            text=True,
            timeout=CALL_TIMEOUT_SECONDS,
            check=True,
        )
        replies[case["id"]] = reply_text(completed.stdout)
        logger.info("recorded matching reply case=%s", case["id"])
    return replies


def main() -> None:
    logging.basicConfig(level=logging.INFO)
    replies = record(sys.argv[1:])
    REPLIES_PATH.write_text(
        json.dumps(replies, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
