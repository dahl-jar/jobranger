from __future__ import annotations

import logging
import time
from typing import TYPE_CHECKING
from urllib.parse import urlsplit

import httpx

if TYPE_CHECKING:
    from collections.abc import Mapping

logger = logging.getLogger(__name__)

RETRY_STATUSES = frozenset({429, 500, 502, 503, 504})
DEFAULT_DELAYS: tuple[float, ...] = (5.0, 20.0)


def get_with_retry(
    client: httpx.Client,
    url: str,
    *,
    params: Mapping[str, str | int] | None = None,
    headers: Mapping[str, str] | None = None,
    delays: tuple[float, ...] = DEFAULT_DELAYS,
) -> httpx.Response:
    host = urlsplit(url).netloc
    for attempt, delay in enumerate(delays, start=1):
        try:
            response = client.get(url, params=params, headers=headers)
        except httpx.TransportError as error:
            reason = type(error).__name__
        else:
            if response.status_code not in RETRY_STATUSES:
                response.raise_for_status()
                return response
            reason = str(response.status_code)
        logger.warning(
            "http.retry request failed, retrying in %.0fs attempt=%d host=%s reason=%s",
            delay,
            attempt,
            host,
            reason,
        )
        time.sleep(delay)
    response = client.get(url, params=params, headers=headers)
    response.raise_for_status()
    return response
