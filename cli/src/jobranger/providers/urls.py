from __future__ import annotations

from urllib.parse import urlsplit, urlunsplit

OLLAMA_API_PATH = "/v1"


def ollama_root(url: str) -> str:
    try:
        parts = urlsplit(url)
    except ValueError:
        return url
    path = parts.path.rstrip("/")
    while path.endswith(OLLAMA_API_PATH):
        path = path.removesuffix(OLLAMA_API_PATH).rstrip("/")
    return urlunsplit(parts._replace(path=path))
