from __future__ import annotations

import ipaddress
import logging
import socket
import time
from typing import TYPE_CHECKING
from urllib.parse import urlsplit

import httpx

from jobranger.net.headers import BROWSER_HEADERS

if TYPE_CHECKING:
    from collections.abc import Callable

logger = logging.getLogger(__name__)

STATUS_ACTIVE = "active"
STATUS_EXPIRED = "expired"
STATUS_UNKNOWN = "unknown"
STATUS_SKIPPED = "skipped"
STATUS_BLOCKED = "blocked"
EXPIRED_HTTP_STATUSES = frozenset({404, 410})
EXPIRED_MARKERS: dict[str, tuple[str, ...]] = {
    "thehub.io": ('status:"EXPIRED"', 'status:"STOPPED"'),
}
FINN_HOSTS = frozenset({"www.finn.no", "finn.no"})
FINN_APPLY_PATH_PREFIX = "/job-apply/"
NATIVE_APPLY_HOSTS = FINN_HOSTS | {"login.vend.no"}
WEB_SCHEMES = frozenset({"http", "https"})
MAX_REDIRECTS = 5
MAX_BODY_BYTES = 1_048_576
APPLY_CHECK_DEADLINE_SECONDS = 30.0
SCOPE_SEPARATOR = "%"
FALLBACK_ENCODING = "utf-8"

type Resolver = Callable[[str], list[str]]


def resolve_host(host: str) -> list[str]:
    return [str(info[4][0]) for info in socket.getaddrinfo(host, None)]


def is_public_address(address: str) -> bool:
    try:
        parsed = ipaddress.ip_address(address.split(SCOPE_SEPARATOR, 1)[0])
    except ValueError:
        return False
    if isinstance(parsed, ipaddress.IPv6Address) and parsed.ipv4_mapped is not None:
        parsed = parsed.ipv4_mapped
    return parsed.is_global and not parsed.is_multicast


def is_public_url(url: str, resolve: Resolver = resolve_host) -> bool:
    try:
        parsed = urlsplit(url)
        host = parsed.hostname
    except ValueError:
        return False
    if parsed.scheme not in WEB_SCHEMES or not host:
        return False
    if _is_ip_literal(host):
        return is_public_address(host)
    try:
        addresses = resolve(host)
    except (OSError, UnicodeError):
        return False
    return bool(addresses) and all(is_public_address(item) for item in addresses)


def is_checkable(url: str) -> bool:
    parsed = urlsplit(url)
    if parsed.scheme not in WEB_SCHEMES:
        return False
    if parsed.netloc.casefold() in FINN_HOSTS:
        return parsed.path.startswith(FINN_APPLY_PATH_PREFIX)
    return True


def check_apply_link(
    client: httpx.Client,
    url: str,
    resolve: Resolver = resolve_host,
    *,
    monotonic: Callable[[], float] = time.monotonic,
) -> str:
    deadline = monotonic() + APPLY_CHECK_DEADLINE_SECONDS
    current = url
    for _ in range(MAX_REDIRECTS + 1):
        if not is_public_url(current, resolve):
            logger.warning(
                "apply.check.blocked apply link is not a public web address host=%s",
                _host(current),
            )
            return STATUS_BLOCKED
        remaining = deadline - monotonic()
        if remaining <= 0:
            logger.warning("apply.check.deadline apply check ran out of time")
            return STATUS_UNKNOWN
        try:
            with client.stream(
                "GET",
                current,
                headers=BROWSER_HEADERS,
                follow_redirects=False,
                timeout=remaining,
            ) as response:
                if response.next_request is None:
                    body = _read_capped(response, deadline, monotonic)
                    if body is None:
                        logger.warning(
                            "apply.check.deadline apply page read ran out of time"
                        )
                        return STATUS_UNKNOWN
                    return _status_of(response, body)
                current = str(response.next_request.url)
        except httpx.HTTPError as error:
            logger.warning(
                "apply.check.unreachable apply page did not answer host=%s error=%s",
                _host(current),
                type(error).__name__,
            )
            return STATUS_UNKNOWN
    logger.warning("apply.check.redirects too many redirects host=%s", _host(url))
    return STATUS_UNKNOWN


def _status_of(response: httpx.Response, body: str) -> str:
    if response.status_code in EXPIRED_HTTP_STATUSES:
        return STATUS_EXPIRED
    if response.is_error:
        return STATUS_UNKNOWN
    host = response.url.host.casefold()
    if host in NATIVE_APPLY_HOSTS:
        return STATUS_SKIPPED
    if any(marker in body for marker in _markers_for(host)):
        return STATUS_EXPIRED
    return STATUS_ACTIVE


def _read_capped(
    response: httpx.Response, deadline: float, monotonic: Callable[[], float]
) -> str | None:
    collected = bytearray()
    for chunk in response.iter_bytes():
        collected.extend(chunk)
        if len(collected) >= MAX_BODY_BYTES:
            break
        if monotonic() >= deadline:
            return None
    return _decode(bytes(collected[:MAX_BODY_BYTES]), response.encoding)


def _decode(body: bytes, encoding: str | None) -> str:
    try:
        return body.decode(encoding or FALLBACK_ENCODING, errors="replace")
    except LookupError:
        return body.decode(FALLBACK_ENCODING, errors="replace")


def _is_ip_literal(host: str) -> bool:
    try:
        ipaddress.ip_address(host)
    except ValueError:
        return False
    return True


def _host(url: str) -> str:
    try:
        return urlsplit(url).hostname or ""
    except ValueError:
        return ""


def _markers_for(host: str) -> tuple[str, ...]:
    for domain, markers in EXPIRED_MARKERS.items():
        if host == domain or host.endswith(f".{domain}"):
            return markers
    return ()
