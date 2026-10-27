from __future__ import annotations

import base64
import ipaddress
import re
import string
from datetime import datetime
from urllib.parse import SplitResult, quote, urlsplit, urlunsplit

from jobranger.contract.codec import ContractError
from jobranger.contract.limits import JSON_ENCODING, URL_MAX_CHARS
from jobranger.contract.payload import Timestamp

URL_SCHEMES = ("http", "https")
URL_SAFE_CHARS = "".join(char for char in string.printable if not char.isspace())
DNS_HOST = re.compile(
    r"[a-z0-9]([a-z0-9-]*[a-z0-9])?(\.[a-z0-9]([a-z0-9-]*[a-z0-9])?)*\.?"
)
USERINFO = re.compile(r"[!-\[\]-~]+")
ZONE_SEPARATOR = "%"


def decode_base64(text: str, max_bytes: int) -> bytes:
    try:
        decoded = base64.b64decode(text, validate=True)
    except ValueError as error:
        message = "text is not standard padded base64"
        raise ContractError(message) from error
    if base64.b64encode(decoded).decode("ascii") != text:
        message = "non-canonical base64"
        raise ContractError(message)
    if len(decoded) > max_bytes:
        message = f"decoded {len(decoded)} bytes, max {max_bytes}"
        raise ContractError(message)
    return decoded


def parse_timestamp(text: str) -> datetime:
    try:
        Timestamp.model_validate(text)
        return datetime.fromisoformat(text)
    except ValueError as error:
        message = "timestamp is not an RFC 3339 date-time with offset"
        raise ContractError(message) from error


def encoded_host(parts: SplitResult, hostname: str) -> str:
    if parts.netloc.rpartition("@")[2].startswith("["):
        if ZONE_SEPARATOR in hostname:
            message = "url host carries an IPv6 zone id"
            raise ValueError(message)
        return f"[{ipaddress.IPv6Address(hostname).compressed}]"
    host = hostname.encode("idna").decode("ascii")
    if not DNS_HOST.fullmatch(host):
        message = "url host is not a domain name"
        raise ValueError(message)
    return host


def encoded_netloc(parts: SplitResult, hostname: str) -> str:
    userinfo, has_userinfo, _ = parts.netloc.rpartition("@")
    if has_userinfo and not USERINFO.fullmatch(userinfo):
        message = "url userinfo is not printable ASCII without backslash"
        raise ValueError(message)
    port = parts.port
    if port == 0:
        message = "url port is zero"
        raise ValueError(message)
    netloc = encoded_host(parts, hostname)
    if port is not None:
        netloc = f"{netloc}:{port}"
    return f"{userinfo}@{netloc}" if has_userinfo else netloc


def encode_url(url: str) -> str:
    try:
        url.encode(JSON_ENCODING)
        parts = urlsplit(url)
        if parts.scheme not in URL_SCHEMES:
            message = f"url scheme {parts.scheme!r} is not http or https"
            raise ValueError(message)
        hostname = parts.hostname
        if not hostname:
            message = "url has no host"
            raise ValueError(message)
        netloc = encoded_netloc(parts, hostname)
    except (ValueError, UnicodeError) as error:
        message = f"invalid url: {error}"
        raise ContractError(message) from error
    path, query, fragment = (
        quote(part, safe=URL_SAFE_CHARS)
        for part in (parts.path, parts.query, parts.fragment)
    )
    encoded = urlunsplit((parts.scheme, netloc, path, query, fragment))
    if len(encoded) > URL_MAX_CHARS:
        message = f"url has {len(encoded)} chars, max {URL_MAX_CHARS}"
        raise ContractError(message)
    return encoded
