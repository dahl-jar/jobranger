from __future__ import annotations

import json

import pytest
from factories import FIXTURES, ListSink

from jobranger.matching.profile import Profile, load_profile
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
def fake_profile() -> Profile:
    return load_profile(FIXTURES / "profile_user.yaml")


@pytest.fixture
def no_retry_wait(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(retry.time, "sleep", lambda seconds: None)


@pytest.fixture
def sink() -> ListSink:
    return ListSink()
