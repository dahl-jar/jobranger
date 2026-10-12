from __future__ import annotations

import pytest
from factories import INVALID_URL

from jobranger.providers.urls import ollama_root


@pytest.mark.parametrize(
    ("url", "root"),
    [
        ("http://v1", "http://v1"),
        ("http://v1:11434/v1", "http://v1:11434"),
        ("http://box:11434/full/v1/v1", "http://box:11434/full"),
        ("http://box:11434/full//v1", "http://box:11434/full"),
        (INVALID_URL, INVALID_URL),
    ],
)
def test_should_strip_api_path_from_url_path_only(url: str, root: str) -> None:
    assert ollama_root(url) == root
