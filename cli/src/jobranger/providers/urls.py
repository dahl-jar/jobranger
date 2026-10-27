from __future__ import annotations

import re
from urllib.parse import urlsplit, urlunsplit

OLLAMA_API_PATH = "/v1"
USERINFO_PATTERN = re.compile(r"^(?P<scheme>[A-Za-z][A-Za-z0-9+.-]*:/*).*@", re.DOTALL)


def ollama_root(url: str) -> str:
    try:
        parts = urlsplit(url)
    except ValueError:
        return url
    path = parts.path.rstrip("/")
    while path.endswith(OLLAMA_API_PATH):
        path = path.removesuffix(OLLAMA_API_PATH).rstrip("/")
    return urlunsplit(parts._replace(path=path))


def without_userinfo(url: str) -> str:
    return USERINFO_PATTERN.sub(r"\g<scheme>", url)
