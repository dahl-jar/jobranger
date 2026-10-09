from __future__ import annotations

import base64
import json
from pathlib import Path

from jobranger.ads.models import Ad
from jobranger.matching.classify import Classification
from jobranger.matching.rules import PROFILE_VERSION, Match
from jobranger.sources.registry import AshbyBoard, TeamtailorFeed

FIXTURES = Path(__file__).parent / "fixtures"
ASHBY_BOARD = AshbyBoard(
    key="example-one", company="Org One AS", board_name="Org One AS"
)
TEAMTAILOR_FEED = TeamtailorFeed(
    key="example",
    company="Example AS",
    url="https://careers.example.test/jobs.rss?per_page=200",
)
OTHER_TEAMTAILOR_FEED = TeamtailorFeed(
    "org-two", "Org Two AS", "https://careers.example.test/rss"
)


def classification_payload(**overrides: object) -> dict[str, object]:
    payload = json.loads((FIXTURES / "classification.json").read_text())
    payload.update(overrides)
    return payload


def cli_output(result: str, cost_usd: float = 0.0, **tokens: int) -> str:
    usage = {
        "input_tokens": 0,
        "cache_creation_input_tokens": 0,
        "cache_read_input_tokens": 0,
        "output_tokens": 0,
    }
    usage.update(tokens)
    return json.dumps(
        {
            "type": "result",
            "is_error": False,
            "result": result,
            "total_cost_usd": cost_usd,
            "usage": usage,
        }
    )


def make_match(**overrides: object) -> Match:
    base = dict(classification_payload()["match"], version=PROFILE_VERSION)
    for name in ("evidence", "gaps", "blockers"):
        base[name] = tuple(base[name])
    base.update(overrides)
    return Match(**base)


def make_classification(**overrides: object) -> Classification:
    base = classification_payload()
    for name in ("technologies", "requirements"):
        base[name] = tuple(base[name])
    base["match"] = make_match()
    base.update(overrides)
    return Classification(**base)


def make_ad(**overrides: object) -> Ad:
    base: dict[str, object] = {
        "id": "finn:1234",
        "source": "finn",
        "source_id": "1234",
        "title": "Utvikler",
        "company": "Org One AS",
        "location": "Bergen",
        "published": "2026-08-24T00:00:00+00:00",
        "deadline": None,
        "url": "https://www.finn.no/job/ad/1234",
        "description": None,
        "label": None,
        "is_programming_job": True,
        "display_title": "Software Developer",
        "role_type": "general_software",
        "technologies": (),
        "requirements": (),
        "match": make_match(),
    }
    base.update(overrides)
    return Ad(**base)


def make_search_html(
    docs: list[dict[str, object]], match_count: int, last_page: int = 1
) -> str:
    state = {
        "queries": [
            {
                "state": {
                    "data": {
                        "docs": docs,
                        "metadata": {
                            "result_size": {"match_count": match_count},
                            "paging": {"current": 1, "last": last_page},
                        },
                    }
                }
            }
        ]
    }
    encoded = base64.b64encode(json.dumps(state).encode()).decode()
    return f'<html><script data-react-query-state="1">{encoded}</script></html>'


def make_search_doc(source_id: int, **overrides: object) -> dict[str, object]:
    doc: dict[str, object] = {
        "id": source_id,
        "heading": f"Utvikler {source_id}",
        "company_name": "Org One AS",
        "location": "Bergen",
        "published": 1789394781000,
        "deadline": None,
        "canonical_url": f"https://www.finn.no/job/ad/{source_id}",
    }
    doc.update(overrides)
    return doc


class FakeClock:
    def __init__(self) -> None:
        self.now = 0.0
        self.sleeps: list[float] = []

    def __call__(self) -> float:
        return self.now

    def sleep(self, seconds: float) -> None:
        self.sleeps.append(seconds)
        self.now += seconds
