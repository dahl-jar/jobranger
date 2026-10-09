from __future__ import annotations

from factories import ASHBY_BOARD, OTHER_TEAMTAILOR_FEED

from jobranger.settings import SourcesConfig
from jobranger.sources import finn, linkedin, nav
from jobranger.sources.base import Source
from jobranger.sources.registry import employer_feeds, enabled_sources

FEEDS = (ASHBY_BOARD, OTHER_TEAMTAILOR_FEED)


def test_should_skip_finn_when_disabled_in_config() -> None:
    sources = enabled_sources(SourcesConfig(finn=False, nav=True, employers=False))

    assert [source.name for source in sources] == ["nav"]


def test_should_add_one_source_per_employer_feed() -> None:
    sources = enabled_sources(
        SourcesConfig(nav=False, employers=True, employer_feeds=FEEDS)
    )

    assert [source.name for source in sources] == [
        "ashby:example-one",
        "teamtailor:org-two",
    ]


def test_should_order_finn_nav_linkedin_before_feeds() -> None:
    sources = enabled_sources(
        SourcesConfig(finn=True, linkedin=True, employer_feeds=FEEDS[:1])
    )

    assert [source.name for source in sources] == [
        "finn",
        "nav",
        "linkedin",
        "ashby:example-one",
    ]


def test_should_list_no_feeds_for_vetoes_when_employers_off() -> None:
    assert employer_feeds(SourcesConfig(employers=False, employer_feeds=FEEDS)) == ()


def test_should_wire_each_source_to_its_pull() -> None:
    sources = enabled_sources(SourcesConfig(finn=True, linkedin=True, employers=False))

    assert sources == [
        Source("finn", finn.pull),
        Source("nav", nav.pull),
        Source("linkedin", linkedin.pull),
    ]
