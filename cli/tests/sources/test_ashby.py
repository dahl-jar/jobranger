from __future__ import annotations

import json

import httpx
import pytest
from factories import ASHBY_BOARD, FIXTURES

from jobranger.sources.ashby import AshbyParseError, fetch_board, parse_board

BOARD = ASHBY_BOARD


def test_should_parse_listed_norwegian_jobs() -> None:
    payload = json.loads((FIXTURES / "ashby_board.json").read_text())
    payload["jobs"] = [payload["jobs"][0]]

    snapshot = parse_board(payload, BOARD)

    assert [ad.id for ad in snapshot.ads] == ["ashby:example-one:ash-1"]
    assert snapshot.active_ids == frozenset({"ashby:example-one:ash-1"})
    first = snapshot.ads[0]
    assert first.source == "ashby:example-one"
    assert first.source_id == "ash-1"
    assert first.company == "Org One AS"
    assert first.location == "Oslo"
    assert first.published == "2026-08-20T10:00:00.000Z"
    assert first.url == "https://jobs.ashbyhq.com/example-one/ash-1"
    assert first.description == "Build Python services."


def test_should_use_norwegian_secondary_location() -> None:
    payload = json.loads((FIXTURES / "ashby_board.json").read_text())
    payload["jobs"] = [payload["jobs"][1]]

    snapshot = parse_board(payload, BOARD)

    assert snapshot.ads[0].location == "Bergen"


def test_should_exclude_foreign_jobs() -> None:
    payload = json.loads((FIXTURES / "ashby_board.json").read_text())
    payload["jobs"] = [payload["jobs"][2]]

    snapshot = parse_board(payload, BOARD)

    assert snapshot.ads == []


def test_should_exclude_unlisted_jobs() -> None:
    payload = json.loads((FIXTURES / "ashby_board.json").read_text())
    payload["jobs"] = [payload["jobs"][3]]

    snapshot = parse_board(payload, BOARD)

    assert snapshot.ads == []


def test_should_reject_payload_without_jobs() -> None:
    with pytest.raises(AshbyParseError):
        parse_board({}, BOARD)


def test_should_reject_job_without_identity() -> None:
    with pytest.raises(AshbyParseError):
        parse_board({"jobs": [{}]}, BOARD)


@pytest.mark.usefixtures("no_retry_wait")
def test_should_retry_ashby_board_once_on_503() -> None:
    payload = (FIXTURES / "ashby_board.json").read_text()
    responses = iter([httpx.Response(503), httpx.Response(200, text=payload)])
    attempts: list[httpx.Request] = []

    def respond(request: httpx.Request) -> httpx.Response:
        attempts.append(request)
        return next(responses)

    client = httpx.Client(transport=httpx.MockTransport(respond))

    snapshot = fetch_board(client, BOARD)

    assert [ad.id for ad in snapshot.ads] == [
        "ashby:example-one:ash-1",
        "ashby:example-one:ash-2",
    ]
    assert len(attempts) == 2
