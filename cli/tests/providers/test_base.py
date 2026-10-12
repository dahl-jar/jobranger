from __future__ import annotations

from jobranger.providers.base import require_key

EDGE_ASCII_KEY = "!fake-key~"


def test_should_accept_key_with_printable_ascii_edges() -> None:
    assert require_key("anthropic", EDGE_ASCII_KEY) == EDGE_ASCII_KEY
