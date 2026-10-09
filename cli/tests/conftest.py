from __future__ import annotations

import json

import pytest
from factories import FIXTURES

from jobranger import exclusions
from jobranger.net import retry


@pytest.fixture
def finn_search_html() -> str:
    return (FIXTURES / "finn_search.html").read_text()


@pytest.fixture
def finn_ad_html() -> str:
    return (FIXTURES / "finn_ad.html").read_text()


@pytest.fixture
def nav_entry() -> dict[str, object]:
    return json.loads((FIXTURES / "nav_entry.json").read_text())


@pytest.fixture
def excluded_company(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(exclusions, "EXCLUDED_COMPANIES", ("Vestland Bootcamp",))


@pytest.fixture
def no_retry_wait(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(retry.time, "sleep", lambda seconds: None)
