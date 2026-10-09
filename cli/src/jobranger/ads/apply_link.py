from __future__ import annotations

import logging
from urllib.parse import urlsplit

import httpx

from jobranger.net.headers import BROWSER_HEADERS

logger = logging.getLogger(__name__)

STATUS_ACTIVE = "active"
STATUS_EXPIRED = "expired"
STATUS_UNKNOWN = "unknown"
STATUS_SKIPPED = "skipped"
EXPIRED_HTTP_STATUSES = frozenset({404, 410})
EXPIRED_MARKERS: dict[str, tuple[str, ...]] = {
    "thehub.io": ('status:"EXPIRED"', 'status:"STOPPED"'),
}
FINN_HOSTS = frozenset({"www.finn.no", "finn.no"})
FINN_APPLY_PATH_PREFIX = "/job-apply/"
NATIVE_APPLY_HOSTS = FINN_HOSTS | {"login.vend.no"}
WEB_SCHEMES = frozenset({"http", "https"})


def is_checkable(url: str) -> bool:
    parsed = urlsplit(url)
    if parsed.scheme not in WEB_SCHEMES:
        return False
    if parsed.netloc.casefold() in FINN_HOSTS:
        return parsed.path.startswith(FINN_APPLY_PATH_PREFIX)
    return True


def check_apply_link(client: httpx.Client, url: str) -> str:
    try:
        response = client.get(url, headers=BROWSER_HEADERS)
    except httpx.TransportError as error:
        logger.warning(
            "apply.check.unreachable apply page did not answer host=%s error=%s",
            urlsplit(url).netloc,
            type(error).__name__,
        )
        return STATUS_UNKNOWN
    if response.status_code in EXPIRED_HTTP_STATUSES:
        return STATUS_EXPIRED
    if response.is_error:
        return STATUS_UNKNOWN
    host = urlsplit(str(response.url)).netloc.casefold()
    if host in NATIVE_APPLY_HOSTS:
        return STATUS_SKIPPED
    if any(marker in response.text for marker in _markers_for(host)):
        return STATUS_EXPIRED
    return STATUS_ACTIVE


def _markers_for(host: str) -> tuple[str, ...]:
    for domain, markers in EXPIRED_MARKERS.items():
        if host == domain or host.endswith(f".{domain}"):
            return markers
    return ()
