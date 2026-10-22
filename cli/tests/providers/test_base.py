from __future__ import annotations

import pytest

from jobranger.providers.base import (
    KeyCheck,
    ProviderError,
    ProviderErrorKind,
    StopKind,
    Usage,
    finish_result,
    rejected_key,
    require_key,
)

EDGE_ASCII_KEY = "!fake-edges~"
SHORT_KEY = EDGE_ASCII_KEY[1:]
OUTSIDE_ASCII_KEYS = ("fake key-12345", "fake\x7fkey-12345")


def test_should_accept_key_with_printable_ascii_edges() -> None:
    assert require_key("anthropic", EDGE_ASCII_KEY) == EDGE_ASCII_KEY


@pytest.mark.parametrize("key", OUTSIDE_ASCII_KEYS, ids=["space", "delete"])
def test_should_refuse_key_with_char_outside_printable_ascii(key: str) -> None:
    with pytest.raises(ValueError, match="^OpenAI API key must be printable ASCII$"):
        require_key("openai", key)


def test_should_refuse_key_shorter_than_twelve_chars() -> None:
    with pytest.raises(ValueError, match="^OpenAI API key is too short$"):
        require_key("openai", SHORT_KEY)


def test_should_keep_detail_with_eleven_chars_of_twelve_char_key() -> None:
    body = {"error": {"message": f"unknown {SHORT_KEY}"}}

    check = rejected_key(
        ProviderErrorKind.bad_request, "OpenAI", "model", body, EDGE_ASCII_KEY
    )

    assert check == KeyCheck(
        ok=False,
        message=(
            "OpenAI refused the key check. Check the key and its workspace or "
            f"project settings. OpenAI said: unknown {SHORT_KEY}"
        ),
    )


@pytest.mark.parametrize(
    ("stop", "text"),
    [(None, "42"), (None, "not json"), (StopKind.refusal, "I can't help.")],
    ids=["parsed", "invalid", "stopped"],
)
def test_should_keep_reply_text_in_result(stop: StopKind | None, text: str) -> None:
    result = finish_result(text, Usage(calls=1), stop, int)

    assert result.text == text


def test_should_show_kind_and_message_in_error_text() -> None:
    error = ProviderError(ProviderErrorKind.auth, "no key")

    assert str(error) == "auth: no key"
