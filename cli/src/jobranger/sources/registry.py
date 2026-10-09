from __future__ import annotations

from functools import partial
from typing import TYPE_CHECKING

from jobranger.sources import ashby, finn, linkedin, nav, teamtailor
from jobranger.sources.base import Source

if TYPE_CHECKING:
    from jobranger.catalogue import EmployerFeed
    from jobranger.settings import SourcesConfig

ASHBY_KIND = "ashby"


def enabled_sources(sources: SourcesConfig) -> list[Source]:
    enabled: list[Source] = []
    if sources.finn:
        enabled.append(Source(finn.SOURCE, finn.pull))
    if sources.nav:
        enabled.append(Source(nav.SOURCE, nav.pull))
    if sources.linkedin:
        enabled.append(Source(linkedin.SOURCE, linkedin.pull))
    for feed in employer_feeds(sources):
        pull = ashby.pull_feed if feed.kind == ASHBY_KIND else teamtailor.pull_feed
        enabled.append(Source(feed.source, partial(pull, feed=feed)))
    return enabled


def employer_feeds(sources: SourcesConfig) -> tuple[EmployerFeed, ...]:
    return sources.employer_feeds if sources.employers else ()
