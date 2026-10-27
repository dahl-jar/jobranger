from __future__ import annotations

import hashlib
import logging
from dataclasses import dataclass
from datetime import UTC
from typing import TYPE_CHECKING

from jobranger.credentials import get_secret
from jobranger.logging_setup import LogValue, log_event
from jobranger.notify.digest import build_digest
from jobranger.notify.email import DigestLinks, render_digest
from jobranger.notify.send import SECRET_NAMES, EmailError, send_email
from jobranger.site import SITE_URL

if TYPE_CHECKING:
    from collections.abc import Sequence
    from datetime import datetime

    import httpx

    from jobranger.notify.digest import DigestData
    from jobranger.notify.email import RenderedDigest
    from jobranger.pipeline import RunResult
    from jobranger.settings import AppConfig
    from jobranger.storage.store import Store

logger = logging.getLogger(__name__)

KEY_SEPARATOR = "\0"


@dataclass(frozen=True)
class Delivered:
    email_to: str | None = None
    email_error: str | None = None


def deliver(
    config: AppConfig,
    store: Store,
    result: RunResult,
    client: httpx.Client,
    now: datetime,
) -> Delivered:
    email = config.notify.email
    _mark_reported(store, result.silent_ids, now)
    if not email.on:
        _mark_reported(store, result.reported_ids, now)
        return Delivered()
    data = build_digest(store, result, config, now.astimezone().date())
    if not _has_news(data):
        return Delivered()
    site_url = SITE_URL if config.notify.website else None
    rendered = render_digest(data, DigestLinks(site_url), config.targets)
    secret = get_secret(SECRET_NAMES[email.via])
    try:
        send_email(email, secret, rendered, client, _idempotency_key(result, rendered))
    except EmailError as error:
        _log_failure(error)
        return Delivered(email_error=error.reason)
    _mark_reported(store, rendered.ad_ids, now)
    log_event(logger, logging.INFO, "email.sent", count=len(rendered.ad_ids))
    return Delivered(email_to=email.address)


def _log_failure(error: EmailError) -> None:
    cause = error.__cause__ or error
    fields: dict[str, LogValue] = {
        "error_type": type(cause).__name__,
        "failure": error.kind,
    }
    if error.status is not None:
        fields["status"] = error.status
    log_event(logger, logging.WARNING, "email.failed", **fields)


def _has_news(data: DigestData) -> bool:
    return bool(
        data.apply_soon
        or data.new_today
        or data.carried
        or data.failed_sources
        or data.budget_paused
        or data.model_stopped is not None
    )


def _idempotency_key(result: RunResult, rendered: RenderedDigest) -> str:
    content = KEY_SEPARATOR.join(
        (str(result.run_id), rendered.subject, rendered.text, rendered.html)
    )
    return hashlib.sha256(content.encode()).hexdigest()


def _mark_reported(store: Store, ad_ids: Sequence[str], now: datetime) -> None:
    store.mark_reported(list(ad_ids), now.astimezone(UTC).isoformat())
