from __future__ import annotations

import pytest
from factories import CREDENTIAL_OLLAMA_URL, INVALID_URL, OLLAMA_URL

from jobranger.providers.urls import ollama_root, without_userinfo


@pytest.mark.parametrize(
    ("url", "root"),
    [
        ("http://v1", "http://v1"),
        ("http://v1:11434/v1", "http://v1:11434"),
        ("http://box:11434/full/v1/v1", "http://box:11434/full"),
        ("http://box:11434/full//v1", "http://box:11434/full"),
        ("http://box:11434/proxyX", "http://box:11434/proxyX"),
        ("http://box:11434/proxyX/v1", "http://box:11434/proxyX"),
        (INVALID_URL, INVALID_URL),
    ],
)
def test_should_strip_api_path_from_url_path_only(url: str, root: str) -> None:
    assert ollama_root(url) == root


@pytest.mark.parametrize(
    ("url", "shown"),
    [
        (f"{CREDENTIAL_OLLAMA_URL}/v1", f"{OLLAMA_URL}/v1"),
        ("http://user@box:11434", OLLAMA_URL),
        ("http://user:p@ss@box:11434", OLLAMA_URL),
        ("http://user:Qa/Secret#Pw42@box:11434", OLLAMA_URL),
        ("http://user:Qa?Secret@box:11434", OLLAMA_URL),
        ("http:/user:Secret@box:11434", "http:/box:11434"),
        ("HTTP://user:Qa/Secret@box:11434", "HTTP://box:11434"),
        ("http://user:pass@[::1", INVALID_URL),
        (OLLAMA_URL, OLLAMA_URL),
        ("box:11434", "box:11434"),
    ],
    ids=[
        "password",
        "user only",
        "at sign in password",
        "slash and hash in password",
        "question mark in password",
        "single slash",
        "upper-case scheme",
        "invalid url",
        "no userinfo",
        "no scheme",
    ],
)
def test_should_hide_userinfo_in_shown_url(url: str, shown: str) -> None:
    assert without_userinfo(url) == shown
