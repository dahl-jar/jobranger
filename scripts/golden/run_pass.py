from __future__ import annotations

import argparse
import itertools
import json
import os
import sqlite3
import tempfile
from datetime import UTC, datetime, timedelta
from pathlib import Path
from urllib.parse import urlsplit, urlunsplit

import httpx
from jobranger import pipeline
from jobranger.ads import apply_link
from jobranger.events import CancelToken, NullSink, RunMode
from jobranger.matching.assessment import AdAssessment
from jobranger.net import retry
from jobranger.providers.base import (
    ClassifyPrompt,
    ProviderResult,
    StopKind,
    Usage,
)
from jobranger.settings import load_config
from jobranger.storage import store as store_module
from jobranger.storage.store import Store

RUN_START = datetime(2026, 10, 9, 12, 0, tzinfo=UTC)
CLOCK_STEP = timedelta(milliseconds=1)
PROFILE = "Matching/profile_user.yaml"
REPLY_USAGE = Usage(calls=1, input_tokens=100, output_tokens=20, cost_usd=0.01)
NULL_TEXT = "NULL"
COLUMN_SEPARATOR = "|"
NOT_FOUND = 404
DIR_VARIABLES = ("JOBRANGER_CONFIG_DIR", "JOBRANGER_DATA_DIR", "JOBRANGER_STATE_DIR")


class FrozenDatetime(datetime):
    @classmethod
    def now(cls, tz: object = None) -> datetime:
        return RUN_START


class KeyedProvider:
    def __init__(self, replies: dict[str, str]) -> None:
        self.replies = replies

    @property
    def name(self) -> str:
        return "anthropic"

    @property
    def model(self) -> str:
        return "claude-haiku-4-5"

    def classify(self, prompt: ClassifyPrompt, schema: type) -> ProviderResult:
        ad_id = json.loads(prompt.user.rstrip().splitlines()[-1])["id"]
        text = self.replies[ad_id]
        parsed = AdAssessment.model_validate_json(text)
        return ProviderResult(
            text=text, parsed=parsed, usage=REPLY_USAGE, stop=StopKind.done
        )

    def check_key(self) -> None:
        raise NotImplementedError

    def batch_client(self) -> None:
        return None


def pin_clocks() -> None:
    ticks = itertools.count(1)

    def tick() -> str:
        return (RUN_START + next(ticks) * CLOCK_STEP).isoformat()

    store_module._now = tick
    pipeline.datetime = FrozenDatetime
    apply_link.is_public_url = lambda url, resolve=None: True
    retry.time.sleep = lambda seconds: None


def site_transport(inputs: Path) -> httpx.MockTransport:
    routes = json.loads((inputs / "routes.json").read_text(encoding="utf-8"))
    by_key = {route_key(route["url"]): route for route in routes}

    def answer(request: httpx.Request) -> httpx.Response:
        url = str(request.url)
        route = by_key.get(query_key(url)) or by_key.get(path_key(url))
        if route is None:
            return httpx.Response(NOT_FOUND)
        return httpx.Response(
            route["status"],
            headers={"content-type": route["type"]},
            content=(inputs / route["file"]).read_bytes(),
        )

    return httpx.MockTransport(answer)


def path_key(url: str) -> str:
    parts = urlsplit(url)
    return urlunsplit((parts.scheme, parts.netloc, parts.path, "", ""))


def query_key(url: str) -> str:
    parts = urlsplit(url)
    return urlunsplit((parts.scheme, parts.netloc, parts.path, parts.query, ""))


def route_key(url: str) -> str:
    return query_key(url) if urlsplit(url).query else path_key(url)


def cell(value: object) -> str:
    return NULL_TEXT if value is None else str(value)


def table_rows(db_path: Path, queries: dict[str, str]) -> dict[str, list[str]]:
    connection = sqlite3.connect(db_path)
    try:
        return {
            name: [
                COLUMN_SEPARATOR.join(cell(value) for value in row)
                for row in connection.execute(sql)
            ]
            for name, sql in queries.items()
        }
    finally:
        connection.close()


def run(inputs: Path, work: Path) -> dict[str, object]:
    for variable in DIR_VARIABLES:
        os.environ[variable] = str(work / variable.lower())
    pin_clocks()
    config = load_config(inputs / "config.yaml")
    profile = config.profile.model_copy(update={"path": inputs.parent / PROFILE})
    config = config.model_copy(update={"profile": profile})
    replies = json.loads((inputs / "replies.json").read_text(encoding="utf-8"))
    db_path = work / "jobranger.db"
    store = Store(db_path)
    with httpx.Client(transport=site_transport(inputs)) as http:
        result = pipeline.run_pass(
            config,
            store,
            NullSink(),
            CancelToken(),
            mode=RunMode.manual,
            provider=KeyedProvider(replies),
            http=http,
        )
    queries = json.loads((inputs / "queries.json").read_text(encoding="utf-8"))
    return {
        "tables": table_rows(db_path, queries),
        "result": {
            "status": result.status,
            "reported_ids": list(result.reported_ids),
            "carried_ids": list(result.carried_ids),
            "silent_ids": list(result.silent_ids),
            "hidden_counts": dict(sorted(result.hidden_counts.items())),
            "sources_read": sorted(result.sources_read),
            "failed_sources": sorted(result.failed_sources),
            "pending": result.pending,
            "model_stopped": result.model_stopped,
        },
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="Write the Python run pass golden")
    parser.add_argument("--inputs", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    with tempfile.TemporaryDirectory(prefix="jobranger-run-pass-") as temp:
        rows = run(args.inputs, Path(temp))
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(
        json.dumps(rows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
