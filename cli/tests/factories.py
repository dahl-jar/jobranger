from __future__ import annotations

import base64
import json
import threading
from pathlib import Path

import httpx

from jobranger.ads.models import Ad
from jobranger.catalogue import EmployerFeed, source_plan
from jobranger.events import CancelToken, RunEvent
from jobranger.matching.assessment import AdAssessment
from jobranger.matching.classify import Classification
from jobranger.matching.rules import Match
from jobranger.providers.base import (
    ClassifyPrompt,
    ProviderError,
    ProviderResult,
    StopKind,
    Usage,
)
from jobranger.settings import Limits, Targets
from jobranger.sources.base import FetchContext

FIXTURES = Path(__file__).parent / "fixtures"
FAKE_VERSION = "test-version"
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
FAKE_LIMITS = Limits(
    levels=frozenset({"junior", "mid", "none"}),
    years=frozenset({"0-1"}),
    hide_masters=True,
)
CALL_USAGE = Usage(calls=1, input_tokens=100, output_tokens=20, cost_usd=0.01)
ASHBY_BOARD = EmployerFeed(
    kind="ashby", key="example-one", company="Org One AS", board="Org One AS"
)
TEAMTAILOR_FEED = EmployerFeed(
    kind="teamtailor",
    key="example",
    company="Example AS",
    board="https://careers.example.test/jobs.rss?per_page=200",
)
OTHER_TEAMTAILOR_FEED = EmployerFeed(
    kind="teamtailor",
    key="org-two",
    company="Org Two AS",
    board="https://careers.example.test/rss",
)


def targets(**overrides: object) -> Targets:
    base: dict[str, object] = {"subfields": ALL_ROLES, "limits": FAKE_LIMITS}
    base.update(overrides)
    return Targets.model_validate(base)


def classification_payload(**overrides: object) -> dict[str, object]:
    payload = json.loads((FIXTURES / "classification.json").read_text())
    payload.update(overrides)
    return payload


def make_assessment(**overrides: object) -> AdAssessment:
    return AdAssessment.model_validate(classification_payload(**overrides))


def make_match(**overrides: object) -> Match:
    base = dict(classification_payload()["match"], version=FAKE_VERSION)
    for name in ("evidence", "gaps", "blockers"):
        base[name] = tuple(base[name])
    base.update(overrides)
    return Match(**base)


def make_classification(**overrides: object) -> Classification:
    base = classification_payload()
    for name in ("technologies", "requirements"):
        base[name] = tuple(base[name])
    base["match"] = make_match()
    base["checks"] = ()
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


type Reply = (
    AdAssessment | ProviderError | StopKind | Exception | tuple[StopKind, AdAssessment]
)


class FakeProvider:
    def __init__(self, replies: list[Reply] | None = None) -> None:
        self.replies = list(replies or [])
        self.prompts: list[ClassifyPrompt] = []
        self.schemas: list[type] = []

    def classify[SchemaT](
        self, prompt: ClassifyPrompt, schema: type[SchemaT]
    ) -> ProviderResult[SchemaT]:
        self.prompts.append(prompt)
        self.schemas.append(schema)
        reply = self.replies.pop(0) if self.replies else make_assessment()
        if isinstance(reply, tuple):
            stop, body = reply
            return ProviderResult(body.model_dump_json(), body, CALL_USAGE, stop)
        if isinstance(reply, Exception):
            raise reply
        if isinstance(reply, StopKind):
            return ProviderResult("", None, CALL_USAGE, reply)
        return ProviderResult(reply.model_dump_json(), reply, CALL_USAGE, StopKind.done)


class ListSink:
    def __init__(self) -> None:
        self.events: list[RunEvent] = []
        self._lock = threading.Lock()

    def emit(self, event: RunEvent) -> None:
        with self._lock:
            self.events.append(event)


RUN_STARTED = "2026-10-07T03:00:00+00:00"


def fetch_context(http: httpx.Client, **overrides: object) -> FetchContext:
    base: dict[str, object] = {
        "http": http,
        "plan": source_plan(targets()),
        "run_started": RUN_STARTED,
        "known_ids": frozenset(),
        "cursors": {},
        "cancel": CancelToken(),
    }
    base.update(overrides)
    return FetchContext(**base)
