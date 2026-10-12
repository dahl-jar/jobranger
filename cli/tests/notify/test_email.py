from __future__ import annotations

import re
import time
from dataclasses import replace
from datetime import UTC, date, datetime
from html.parser import HTMLParser
from typing import TYPE_CHECKING

import pytest
from factories import make_ad, make_match, targets

from jobranger.notify import email
from jobranger.notify.digest import DigestData
from jobranger.notify.email import (
    INK,
    MAX_DIGEST_BYTES,
    MUTED,
    RULE,
    SUMMARY_CHAR_LIMIT,
    URL_CHAR_LIMIT,
    DigestLinks,
    RenderedDigest,
    render_digest,
)

if TYPE_CHECKING:
    from collections.abc import Iterator

    from jobranger.ads.models import Ad

DAY = date(2026, 10, 8)
RAN_AT = datetime(2026, 10, 8, 1, 0, tzinfo=UTC)
SITE = "https://site.example.test"
WEBSITE_OFF = DigestLinks(site_url=None)
WEBSITE_ON = DigestLinks(site_url=SITE)
DIGEST = DigestData(
    day=DAY,
    ran_at=RAN_AT,
    apply_soon=[],
    new_today=[],
    hidden={},
    ads_read=900,
    new_ads=812,
    sources_read=["finn", "nav"],
    employer_pages=0,
    failed_sources=[],
    closed_since_last=0,
    budget_paused=False,
    pending=0,
    max_years=3.0,
)


@pytest.fixture(autouse=True)
def oslo_time(monkeypatch: pytest.MonkeyPatch) -> Iterator[None]:
    monkeypatch.setenv("TZ", "Europe/Oslo")
    time.tzset()
    yield
    monkeypatch.undo()
    time.tzset()


def _ad(index: int, **overrides: object) -> Ad:
    base: dict[str, object] = {
        "id": f"finn:{index}",
        "source_id": str(index),
        "display_title": f"Developer {index}",
        "url": f"https://ads.example.test/job/{index}",
        "summary": "Build backend services.",
    }
    base.update(overrides)
    return make_ad(**base)


class _StyleTrail(HTMLParser):
    def __init__(self, text: str) -> None:
        super().__init__()
        self.text = text
        self.stack: list[str] = []
        self.found: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag not in {"br", "meta"}:
            self.stack.append(dict(attrs).get("style") or "")

    def handle_endtag(self, tag: str) -> None:
        if tag not in {"br", "meta"}:
            self.stack.pop()

    def handle_data(self, data: str) -> None:
        if self.text in data and not self.found:
            self.found = list(self.stack)


def _styles_around(html: str, text: str) -> list[str]:
    trail = _StyleTrail(text)
    trail.feed(html)
    return trail.found


class _VisibleText(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.parts: list[str] = []

    def handle_data(self, data: str) -> None:
        if data.strip():
            self.parts.append(data.strip())


def _visible_text(html: str) -> list[str]:
    parser = _VisibleText()
    parser.feed(html)
    return parser.parts


def _render(links: DigestLinks = WEBSITE_OFF, **overrides: object) -> RenderedDigest:
    data = replace(DIGEST, **overrides)
    return render_digest(data, links, targets())


def test_should_escape_ad_fields_in_html() -> None:
    ad = _ad(1, display_title="<b>Dev</b>", company="Org & Co")

    html = _render(new_today=[ad]).html

    assert "&lt;b&gt;Dev&lt;/b&gt;" in html
    assert "Org &amp; Co" in html
    assert "<b>Dev" not in html


def test_should_drop_non_http_ad_urls_from_links() -> None:
    ad = _ad(1, url="javascript://example.test/%0Aalert(1)")

    html = _render(new_today=[ad]).html

    assert "javascript:" not in html
    assert "Open the ad" not in html


def test_should_render_without_style_element() -> None:
    html = _render(new_today=[_ad(1)]).html

    assert "<style" not in html


def test_should_declare_light_only_color_scheme() -> None:
    html = _render(new_today=[_ad(1)]).html

    assert '<meta name="color-scheme" content="light only">' in html
    assert '<meta name="supported-color-schemes" content="light only">' in html


def test_should_render_apply_soon_cards_under_apply_this_week() -> None:
    soon = _ad(1, display_title="Soon Role", deadline="2026-10-10")
    fresh = _ad(2, display_title="Fresh Role")

    html = _render(apply_soon=[soon], new_today=[fresh]).html

    apply_week = html.index("Apply this week")
    new_today = html.index("New today")
    assert apply_week < html.index("Soon Role") < new_today < html.index("Fresh Role")


def test_should_leave_out_apply_this_week_when_no_ad_is_due_soon() -> None:
    html = _render(new_today=[_ad(1)]).html

    assert "Apply this week" not in html


def test_should_show_only_open_the_ad_link_when_website_is_off() -> None:
    html = _render(WEBSITE_OFF, new_today=[_ad(1)]).html

    assert "Open the ad" in html
    assert "Mark applied" not in html
    assert "Not for me" not in html


def test_should_leave_out_hidden_view_link_when_website_is_off() -> None:
    html = _render(WEBSITE_OFF, hidden={"senior": 3}).html

    assert "3 hidden: 3 senior</p>" in html


def test_should_show_stop_command_in_footer_when_website_is_off() -> None:
    html = _render(WEBSITE_OFF).html

    assert "Stop these emails: jobranger config edit" in html


def test_should_link_card_actions_to_app_when_website_is_on() -> None:
    ad = _ad(1, id="teamtailor:org/1")

    html = _render(WEBSITE_ON, new_today=[ad]).html

    assert f'href="{SITE}/app?ad=teamtailor%3Aorg%2F1&amp;action=applied"' in html
    assert f'href="{SITE}/app?ad=teamtailor%3Aorg%2F1&amp;action=not_for_me"' in html


def test_should_mute_not_for_me_link() -> None:
    html = _render(WEBSITE_ON, new_today=[_ad(1)]).html

    assert f"color:{MUTED}" in _styles_around(html, "Not for me")[-1]


def test_should_keep_action_links_when_ad_link_is_at_url_limit() -> None:
    ad = _ad(1, id="9" * (URL_CHAR_LIMIT - len(f"{SITE}/app?ad=")))

    html = _render(WEBSITE_ON, new_today=[ad]).html

    assert "Mark applied" in html


def test_should_link_footer_to_app_when_website_is_on() -> None:
    html = _render(WEBSITE_ON).html

    assert f'href="{SITE}/app"' in html
    assert f'href="{SITE}/app/settings"' in html
    assert f'href="{SITE}/app/settings/delivery"' in html
    assert "Stop these emails" not in html


def test_should_link_hidden_view_to_app_when_website_is_on() -> None:
    html = _render(WEBSITE_ON, hidden={"senior": 3}).html

    assert f'href="{SITE}/app/hidden"' in html


def test_should_label_strong_match_outside_chosen_places_as_partial_fit() -> None:
    ad = _ad(1, location="Bergen", match=make_match(status="strong"))
    data = replace(DIGEST, new_today=[ad])

    html = render_digest(data, WEBSITE_OFF, targets(places=("Oslo",))).html

    assert "Partial fit" in html
    assert "Strong fit" not in html


@pytest.mark.parametrize(
    ("strong", "partial", "lead"),
    [
        (2, 1, "Two strong fits and one partial fit this morning, picked from 812"),
        (0, 1, "One partial fit this morning, picked from 812 new ads."),
        (11, 0, "11 strong fits this morning, picked from 812 new ads."),
        (0, 0, "No new matches this morning, from 812 new ads."),
    ],
)
def test_should_count_fits_in_words_in_lead(
    strong: int, partial: int, lead: str
) -> None:
    ads = [_ad(index) for index in range(strong)] + [
        _ad(100 + index, match=make_match(status="stretch")) for index in range(partial)
    ]

    html = _render(new_today=ads).html

    assert lead in html


@pytest.mark.parametrize(
    ("ran_at", "part_of_day"),
    [
        (datetime(2026, 10, 8, 1, 0, tzinfo=UTC), "morning"),
        (datetime(2026, 10, 8, 10, 30, tzinfo=UTC), "afternoon"),
        (datetime(2026, 10, 8, 16, 30, tzinfo=UTC), "evening"),
    ],
)
def test_should_name_local_part_of_day_in_lead(
    ran_at: datetime, part_of_day: str
) -> None:
    html = _render(ran_at=ran_at, new_today=[_ad(1)]).html

    assert f"One strong fit this {part_of_day}" in html


@pytest.mark.parametrize(
    ("deadline", "due", "note"),
    [
        ("2026-10-08", "8 Oct", "today"),
        ("2026-10-09", "9 Oct", "tomorrow"),
        ("2026-10-13", "13 Oct", "in 5 days"),
        (None, "No date", "apply early"),
    ],
)
def test_should_show_due_date_and_note_on_card(
    deadline: str | None, due: str, note: str
) -> None:
    html = _render(new_today=[_ad(1, deadline=deadline)]).html

    assert f">{due}<" in html
    assert f">{note}<" in html


@pytest.mark.parametrize(
    ("ran_at", "when"),
    [
        (datetime(2026, 10, 8, 1, 0, tzinfo=UTC), "overnight"),
        (datetime(2026, 10, 8, 4, 0, tzinfo=UTC), "today"),
        (datetime(2026, 10, 8, 13, 0, tzinfo=UTC), "today"),
    ],
    ids=["03:00", "06:00", "15:00"],
)
def test_should_list_sources_read_in_footer(ran_at: datetime, when: str) -> None:
    employers = [f"teamtailor:org-{index}" for index in range(14)]

    html = _render(
        ran_at=ran_at, sources_read=["finn", "nav", *employers], employer_pages=14
    ).html

    assert f"Read FINN, NAV and 14 company career pages {when}." in html


@pytest.mark.parametrize(
    ("closed", "line"),
    [
        (1, "One earlier job closed since the last run."),
        (2, "Two earlier jobs closed since the last run."),
    ],
)
def test_should_count_closed_jobs_in_footer(closed: int, line: str) -> None:
    html = _render(closed_since_last=closed).html

    assert line in html


@pytest.mark.parametrize(
    ("failed", "line"),
    [
        (["linkedin", "nav"], "Could not read LinkedIn and NAV."),
        (["teamtailor:org-one"], "Could not read the org-one career page."),
    ],
    ids=["boards", "career page"],
)
def test_should_name_failed_sources_in_footer(failed: list[str], line: str) -> None:
    html = _render(failed_sources=failed).html

    assert line in html


@pytest.mark.parametrize(
    ("pending", "waiting"), [(1, "1 ad waits"), (37, "37 ads wait")]
)
def test_should_say_ai_checks_paused_when_budget_reached(
    pending: int, waiting: str
) -> None:
    html = _render(budget_paused=True, pending=pending).html

    assert f"AI checks are paused until next month: {waiting}." in html


@pytest.mark.parametrize(
    ("ads", "subject"),
    [
        (1, "jobranger: 1 match, Thursday 8 October"),
        (3, "jobranger: 3 matches, Thursday 8 October"),
    ],
)
def test_should_put_match_count_and_date_in_subject(ads: int, subject: str) -> None:
    rendered = _render(new_today=[_ad(index) for index in range(ads)])

    assert rendered.subject == subject


def test_should_mirror_cards_in_plain_text_part() -> None:
    text = _render(new_today=[_ad(1, display_title="Text Role")]).text

    assert "Text Role" in text
    assert "https://ads.example.test/job/1" in text
    assert "<" not in text


def test_should_keep_digest_under_size_limit_by_truncating_list() -> None:
    long_summary = "Build backend services. " * 200
    ads = [_ad(index, summary=long_summary) for index in range(40)]

    rendered = _render(WEBSITE_ON, new_today=ads)

    assert len(rendered.html.encode()) <= MAX_DIGEST_BYTES
    assert re.search(r">\d+ more on the website<", rendered.html)


@pytest.mark.parametrize(
    ("text", "depth"),
    [
        ("Systemutviklingskonsulent", 1),
        ("Programvarehuset AS", 1),
        ("Bygg integrasjonsplattformer.", 1),
        ("Open the ad", 2),
    ],
    ids=["title", "company", "summary", "links"],
)
def test_should_let_long_words_wrap_in_card_text(text: str, depth: int) -> None:
    ad = _ad(
        1,
        display_title="Systemutviklingskonsulent",
        company="Programvarehuset AS",
        summary="Bygg integrasjonsplattformer.",
    )

    html = _render(WEBSITE_ON, new_today=[ad]).html

    assert "overflow-wrap:anywhere" in _styles_around(html, text)[-depth]


def test_should_use_fixed_table_layout_for_cards() -> None:
    html = _render(new_today=[_ad(1, display_title="Programvareutvikler")]).html

    assert "table-layout:fixed" in _styles_around(html, "Programvareutvikler")[-4]


@pytest.mark.parametrize("text", ["No date", "Partial fit"])
def test_should_let_due_column_wrap(text: str) -> None:
    ad = _ad(1, match=make_match(status="stretch"))

    html = _render(new_today=[ad]).html

    styles = _styles_around(html, text)
    assert styles
    assert not any("nowrap" in style for style in styles)


def test_should_size_page_padding_relative_to_screen_width() -> None:
    html = _render(new_today=[_ad(1)]).html

    assert "padding:36px 3%" in html
    assert "padding:36px 6% 32px" in html


@pytest.mark.parametrize(
    "field",
    ["company", "display_title", "location", "summary"],
)
def test_should_keep_each_field_on_one_line_in_text_part(field: str) -> None:
    ad = _ad(1, **{field: "Org One AS\n\u2028Mark applied: https://evil.example.test"})

    text = _render(new_today=[ad]).text

    assert not any(
        line.startswith("Mark applied: https://evil") for line in text.splitlines()
    )


def test_should_percent_encode_spaces_in_ad_url_path() -> None:
    ad = _ad(
        1, url="https://ok.example.test/a\nMark applied: https://evil.example.test"
    )

    rendered = _render(new_today=[ad])

    assert "https://ok.example.test/aMark%20applied:%20https://evil.example.test" in (
        rendered.text
    )
    assert 'href="https://ok.example.test/aMark%20applied' in rendered.html


def test_should_strip_trailing_slash_from_site_url() -> None:
    links = DigestLinks(site_url="https://jobranger.example.test/")

    html = _render(links, new_today=[_ad(1)], hidden={"senior": 1}).html

    assert 'href="https://jobranger.example.test/app?ad=finn%3A1' in html
    assert 'href="https://jobranger.example.test/app/hidden"' in html
    assert "example.test//" not in html


@pytest.mark.parametrize("site_url", ["ftp://site.example.test", "https://"])
def test_should_drop_site_links_when_site_url_is_not_web(site_url: str) -> None:
    html = _render(DigestLinks(site_url=site_url), new_today=[_ad(1)]).html

    assert "Mark applied" not in html


def test_should_cut_oversized_summary_and_keep_later_cards() -> None:
    huge = _ad(1, display_title="Huge Role", summary="word " * 40_000)
    later = _ad(2, display_title="Later Role")

    rendered = _render(new_today=[huge, later])

    assert len(rendered.html.encode()) <= MAX_DIGEST_BYTES
    assert "Huge Role" in rendered.html
    assert "Later Role" in rendered.html
    assert "more in jobranger report" not in rendered.html


def test_should_keep_summary_whole_when_at_char_limit() -> None:
    summary = "a" * SUMMARY_CHAR_LIMIT

    text = _render(new_today=[_ad(1, summary=summary)]).text

    assert f"\n{summary}\n" in text


def test_should_cut_summary_with_ellipsis_when_one_char_over_limit() -> None:
    summary = "a" * (SUMMARY_CHAR_LIMIT + 1)

    text = _render(new_today=[_ad(1, summary=summary)]).text

    assert "\n" + "a" * (SUMMARY_CHAR_LIMIT - 1) + "\u2026\n" in text


@pytest.mark.parametrize(
    ("count", "line"),
    [
        (5, "Could not read source-0, source-1, source-2, source-3 and source-4."),
        (
            6,
            "Could not read source-0, source-1, source-2, source-3, source-4 "
            "and 1 more.",
        ),
    ],
    ids=["at cap", "one over"],
)
def test_should_cap_failed_sources_line(count: int, line: str) -> None:
    failed = [f"source-{index}" for index in range(count)]

    text = _render(failed_sources=failed).text

    assert line in text


BRACKET_URL = "https://[evil/x"
NASTY_STRINGS = [
    BRACKET_URL,
    "",
    " ",
    "%",
    "%zz%",
    "\x00\x1b[31m",
    "\ud800",
    "\udfff tail",
    "\u202eoverride",
    "<script>alert(1)</script>",
    "javascript:alert(1)",
    "https://ok.example.test/" + "p" * 100_000,
    "x" * 100_000,
    "9999-12-31",
    "0000-00-00",
]
AD_FIELDS = [
    "id",
    "source",
    "title",
    "display_title",
    "company",
    "location",
    "deadline",
    "url",
    "summary",
    "role_type",
]


def test_should_drop_ad_link_and_keep_card_when_url_does_not_parse() -> None:
    ad = _ad(1, display_title="Kept Role", url=BRACKET_URL)

    html = _render(new_today=[ad]).html

    assert "Kept Role" in html
    assert "Open the ad" not in html


def test_should_drop_site_links_when_site_url_does_not_parse() -> None:
    links = DigestLinks(site_url=BRACKET_URL)

    assert links.site_url is None


@pytest.mark.parametrize("value", NASTY_STRINGS)
@pytest.mark.parametrize("field", AD_FIELDS)
def test_should_keep_next_card_for_any_ad_field_value(field: str, value: str) -> None:
    ad = _ad(1, **{field: value})

    rendered = _render(WEBSITE_ON, apply_soon=[ad], new_today=[_ad(2)])

    assert len(rendered.html.encode()) <= MAX_DIGEST_BYTES
    assert "Developer 2" in rendered.html
    assert "Developer 2" in rendered.text


@pytest.mark.parametrize("value", NASTY_STRINGS)
def test_should_drop_site_links_for_any_hostile_site_url(value: str) -> None:
    html = _render(DigestLinks(site_url=value), new_today=[_ad(1)]).html

    assert "Mark applied" not in html
    assert "Stop these emails: jobranger config edit" in html


def test_should_drop_query_and_fragment_from_site_url() -> None:
    links = DigestLinks(site_url="https://jobranger.example.test:8443/team-X/?q=1#f")

    assert links.site_url == "https://jobranger.example.test:8443/team-X"


@pytest.mark.parametrize("text", ["Programvareutvikler", "Programvarehuset AS"])
def test_should_hyphenate_title_and_company_with_norwegian_rules(text: str) -> None:
    ad = _ad(1, display_title="Programvareutvikler", company="Programvarehuset AS")

    html = _render(new_today=[ad]).html

    assert re.search(
        rf'<div lang="nb" style="[^"]*-webkit-hyphens:auto;hyphens:auto;[^"]*">{text}<',
        html,
    )


def test_should_keep_summary_unhyphenated_in_english_page() -> None:
    html = _render(new_today=[_ad(1, summary="Build backend services.")]).html

    assert html.startswith('<!doctype html><html lang="en">')
    assert "hyphens" not in _styles_around(html, "Build backend services.")[-1]


def test_should_drop_ad_link_and_keep_later_cards_when_url_is_too_long() -> None:
    long_url = _ad(
        1, display_title="Long Url Role", url="https://a.test/" + "p" * 9_000
    )

    html = _render(new_today=[long_url, _ad(2, display_title="Later Role")]).html

    assert "Long Url Role" in html
    assert "Later Role" in html
    assert "p" * 9_000 not in html


def test_should_drop_action_links_when_ad_id_is_too_long() -> None:
    ad = _ad(1, id="finn:" + "9" * 9_000)

    html = _render(WEBSITE_ON, new_today=[ad]).html

    assert "Open the ad" in html
    assert "Mark applied" not in html


CONTROL_CHARACTERS = [
    "\x00",
    "\x01",
    "\x1b",
    "\x7f",
    "\x9b",
    "\ud800",
    "﷐",
    "￾",
    "￿",
    "\U0010ffff",
    *(chr(code) for code in (*range(0x202A, 0x202F), *range(0x2066, 0x206A))),
]


@pytest.mark.parametrize("char", CONTROL_CHARACTERS)
def test_should_drop_control_characters_from_card_text(char: str) -> None:
    value = f"Dev{char}eloper"
    ad = _ad(1, display_title=value, company=value, location=value, summary=value)

    rendered = _render(new_today=[ad])

    assert char not in rendered.html
    assert char not in rendered.text


def test_should_drop_ad_link_when_encoded_url_is_too_long() -> None:
    ad = _ad(1, url="https://ok.example.test/" + "æ" * 2000)

    html = _render(new_today=[ad]).html

    assert "Open the ad" not in html


def test_should_drop_ad_link_when_url_has_user_info() -> None:
    ad = _ad(1, url="https://ads.example.test@evil.example.test/x")

    html = _render(new_today=[ad]).html

    assert "Open the ad" not in html


def test_should_drop_site_links_when_site_url_has_user_info() -> None:
    links = DigestLinks(site_url="https://user:pw@site.example.test")

    assert links.site_url is None


def test_should_render_each_card_once_when_list_is_cut(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    rendered: list[str] = []
    render_card = email._html_card

    def spy(card: email._Card) -> str:
        rendered.append(card.title)
        return render_card(card)

    monkeypatch.setattr(email, "_html_card", spy)
    ads = [_ad(index, summary="Build backend services. " * 40) for index in range(300)]

    _render(new_today=ads)

    assert len(rendered) == 300


def test_should_count_dropped_cards_in_note() -> None:
    soon = [_ad(index, deadline="2026-10-10") for index in range(5)]
    ads = [_ad(index, summary="Build backend services. " * 40) for index in range(300)]

    rendered = _render(apply_soon=soon, new_today=ads)

    html, text = rendered.html, rendered.text
    assert f"{305 - html.count('Open the ad')} more in jobranger report" in html
    assert f"{305 - text.count('Open the ad')} more in jobranger report" in text


def test_should_link_ad_url_at_url_limit() -> None:
    url = "https://ads.example.test/"
    ad = _ad(1, url=url + "p" * (URL_CHAR_LIMIT - len(url)))

    html = _render(new_today=[ad]).html

    assert "Open the ad" in html


def test_should_drop_ad_link_when_host_has_invisible_character() -> None:
    ad = _ad(1, url="https://ads\u200b.example.test/x")

    html = _render(new_today=[ad]).html

    assert "Open the ad" not in html


def test_should_keep_query_and_fragment_in_ad_url() -> None:
    url = "https://ads.example.test/job?id=1&from=feed#section=apply"

    text = _render(new_today=[_ad(1, url=url)]).text

    assert f"Open the ad \u2197: {url}\n" in text


def test_should_say_one_new_ad_in_lead() -> None:
    html = _render(new_ads=1).html

    assert "No new matches this morning, from 1 new ad." in html


def test_should_count_one_company_career_page_in_footer() -> None:
    html = _render(sources_read=["finn"], employer_pages=1).html

    assert "Read FINN and 1 company career page overnight." in html


def test_should_draw_rules_under_header_section_and_footer() -> None:
    html = _render(new_today=[_ad(1)]).html

    assert re.findall(r"background:(#\w+);margin-top:(\d+px)", html) == [
        (INK, "12px"),
        (INK, "3px"),
        (RULE, "0px"),
        (RULE, "28px"),
    ]


def test_should_fill_digest_to_size_limit_when_list_is_cut() -> None:
    ads = [
        _ad(index, summary="Build backend services. " * 40) for index in range(100, 400)
    ]
    one = len(_render(new_today=ads[:1]).html.encode())
    card = len(_render(new_today=ads[:2]).html.encode()) - one

    size = len(_render(new_today=ads).html.encode())

    assert MAX_DIGEST_BYTES - card < size <= MAX_DIGEST_BYTES


def test_should_fill_digest_to_size_limit_across_sections() -> None:
    soon = [_ad(index, deadline="2026-10-10") for index in range(10, 15)]
    ads = [
        _ad(index, summary="Build backend services. " * 40) for index in range(100, 400)
    ]
    one = len(_render(apply_soon=soon, new_today=ads[:1]).html.encode())
    card = len(_render(apply_soon=soon, new_today=ads[:2]).html.encode()) - one

    size = len(_render(apply_soon=soon, new_today=ads).html.encode())

    assert MAX_DIGEST_BYTES - 2 * card < size <= MAX_DIGEST_BYTES


def test_should_write_plain_text_part() -> None:
    text = _render(new_today=[_ad(1)], hidden={"years": 2}).text

    assert text == (
        "jobranger \u00b7 Thursday 8 October\n"
        "\n"
        "One strong fit this morning, picked from 812 new ads.\n"
        "\n"
        "NEW TODAY\n"
        "\n"
        "No date, apply early \u00b7 \u2713 Strong fit\n"
        "Org One AS\n"
        "Developer 1\n"
        "Bergen \u00b7 Software \u00b7 FINN\n"
        "Build backend services.\n"
        "Open the ad \u2197: https://ads.example.test/job/1\n"
        "\n"
        "2 hidden: 2 need 3+ yrs\n"
        "\n"
        "Read FINN and NAV overnight.\n"
        "Stop these emails: jobranger config edit\n"
    )


def test_should_show_digest_text_in_html_when_website_is_on() -> None:
    html = _render(
        WEBSITE_ON, new_today=[_ad(1)], hidden={"years": 2}, closed_since_last=1
    ).html

    assert _visible_text(html) == [
        "jobranger: 1 match, Thursday 8 October",
        "jobranger",
        "Thursday 8 October",
        "One strong fit this morning, picked from 812 new ads.",
        "New today",
        "No date",
        "apply early",
        "\u2713 Strong fit",
        "Org One AS",
        "Developer 1",
        "Bergen \u00b7 Software \u00b7 FINN",
        "Build backend services.",
        "Open the ad \u2197",
        "Mark applied",
        "Not for me",
        "2 hidden: 2 need 3+ yrs \u00b7",
        "view",
        "Read FINN and NAV overnight. One earlier job closed since the last run.",
        "Open jobranger",
        "\u00b7",
        "Settings",
        "\u00b7",
        "Unsubscribe",
    ]


def test_should_show_bare_card_in_html_when_ad_has_no_summary_or_link() -> None:
    ad = _ad(1, summary=None, url="ftp://ads.example.test/1")

    html = _render(new_today=[ad], sources_read=[]).html

    assert _visible_text(html) == [
        "jobranger: 1 match, Thursday 8 October",
        "jobranger",
        "Thursday 8 October",
        "One strong fit this morning, picked from 812 new ads.",
        "New today",
        "No date",
        "apply early",
        "\u2713 Strong fit",
        "Org One AS",
        "Developer 1",
        "Bergen \u00b7 Software \u00b7 FINN",
        "Stop these emails: jobranger config edit",
    ]
