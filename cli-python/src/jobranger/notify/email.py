from __future__ import annotations

import unicodedata
from dataclasses import dataclass, replace
from html import escape
from itertools import islice
from typing import TYPE_CHECKING
from urllib.parse import SplitResult, quote, urlsplit, urlunsplit

from jobranger.ads.apply_link import WEB_SCHEMES
from jobranger.ads.duplicates import DIRECT_SOURCE_PREFIXES, is_direct_source
from jobranger.catalogue import role_label
from jobranger.matching.classify import StopReason
from jobranger.matching.rules import display_fit, parse_date
from jobranger.notify.terminal import MIDDLE_DOT, hidden_line

if TYPE_CHECKING:
    from collections.abc import Sequence
    from datetime import date, datetime

    from jobranger.ads.models import Ad
    from jobranger.matching.rules import DisplayFit
    from jobranger.notify.digest import DigestData
    from jobranger.settings import Targets

MAX_DIGEST_BYTES = 90_000
SUMMARY_CHAR_LIMIT = 1000
FIELD_CHAR_LIMIT = 200
URL_CHAR_LIMIT = 2048
FAILED_SOURCES_SHOWN = 5
ELLIPSIS = "…"
URL_SAFE = "/?:@!$&'()*+,;=%~"
HIDDEN_CATEGORIES = frozenset({"Cc", "Cs", "Cn"})
BIDI_CONTROLS = frozenset(
    chr(code) for code in (*range(0x202A, 0x202F), *range(0x2066, 0x206A))
)
WRAP = "overflow-wrap:anywhere;word-break:break-word;"
HYPHENATE = "-webkit-hyphens:auto;hyphens:auto;"
AD_LANG = "nb"
DUE_COLUMN_PX = 100
PRODUCT_NAME = "jobranger"
FONT = (
    "'JetBrainsMono Nerd Font', 'JetBrains Mono', ui-monospace, Menlo, Consolas, "
    "monospace"
)
TABLE_OPEN = '<table role="presentation" cellpadding="0" cellspacing="0" border="0"'
PAGE_BACKGROUND = "#ECE8E1"
CARD_BACKGROUND = "#F8F6F2"
CARD_BORDER = "#E2D9C8"
INK = "#1E1B17"
MUTED = "#68625A"
BODY_TEXT = "#46413A"
RULE = "#DED8CE"
STRONG_COLOR = "#24774F"
PARTIAL_COLOR = "#92600B"
CHECK_MARK = "✓"
HALF_CIRCLE = "◐"
NORTH_EAST_ARROW = "↗"
FIT_LABELS: dict[DisplayFit, str] = {
    "strong": f"{CHECK_MARK} Strong fit",
    "partial": f"{HALF_CIRCLE} Partial fit",
    "on_request": "Shown on request",
}
FIT_COLORS: dict[DisplayFit, str] = {
    "strong": STRONG_COLOR,
    "partial": PARTIAL_COLOR,
    "on_request": MUTED,
}
NUMBER_WORDS = (
    "no",
    "one",
    "two",
    "three",
    "four",
    "five",
    "six",
    "seven",
    "eight",
    "nine",
    "ten",
)
SOURCE_LABELS = {"finn": "FINN", "nav": "NAV", "linkedin": "LinkedIn"}
CAREER_PAGE_LABEL = "Career page"
NOON_HOUR = 12
EVENING_HOUR = 18
OVERNIGHT_END_HOUR = 6
NO_DATE = "No date"
APPLY_EARLY = "apply early"
APPLY_SOON_TITLE = "Apply this week"
NEW_TODAY_TITLE = "New today"
CARRIED_TITLE = "Still waiting from earlier"
LEAD_FITS: tuple[DisplayFit, ...] = ("strong", "partial", "on_request")
STOP_NOTES: dict[StopReason, str] = {
    StopReason.auth: (
        "AI checks stopped: the provider refused the API key."
        " {pending} {waiting}. Set a new key with: jobranger init"
    ),
    StopReason.provider_down: (
        "AI checks stopped: the provider did not answer."
        " {pending} {waiting} for the next run."
    ),
}
STOP_EMAILS = "Stop these emails: jobranger config edit"


@dataclass(frozen=True)
class DigestLinks:
    site_url: str | None

    def __post_init__(self) -> None:
        object.__setattr__(self, "site_url", _site_root(self.site_url))


@dataclass(frozen=True)
class RenderedDigest:
    subject: str
    html: str
    text: str
    ad_ids: tuple[str, ...] = ()


@dataclass(frozen=True)
class _Link:
    label: str
    href: str
    muted: bool = False


@dataclass(frozen=True)
class _Card:
    ad_id: str
    due: str
    due_note: str
    fit: DisplayFit
    company: str
    title: str
    meta: str
    why: str
    links: tuple[_Link, ...]


@dataclass(frozen=True)
class _Section:
    title: str
    cards: tuple[_Card, ...]
    dropped: int


@dataclass(frozen=True)
class _Digest:
    subject: str
    date_line: str
    lead: str
    sections: tuple[_Section, ...]
    hidden_text: str
    hidden_link: _Link | None
    notes: tuple[str, ...]
    footer_links: tuple[_Link, ...]
    dropped_note: str


def render_digest(
    data: DigestData, links: DigestLinks, targets: Targets
) -> RenderedDigest:
    digest = _digest(data, links, targets)
    cards = [_html_card(card) for section in digest.sections for card in section.cards]
    kept = _kept_count(_html(_keep(digest, 0), []), cards)
    digest = _keep(digest, kept)
    shown = tuple(card.ad_id for section in digest.sections for card in section.cards)
    return RenderedDigest(
        digest.subject, _html(digest, cards[:kept]), _text(digest), shown
    )


def _kept_count(frame: str, cards: Sequence[str]) -> int:
    total = len(frame.encode())
    for kept, card in enumerate(cards):
        total += len(card.encode())
        if total > MAX_DIGEST_BYTES:
            return kept
    return len(cards)


def _keep(digest: _Digest, count: int) -> _Digest:
    sections: list[_Section] = []
    for section in digest.sections:
        cards = section.cards[:count]
        count -= len(cards)
        dropped = len(section.cards) - len(cards)
        sections.append(replace(section, cards=cards, dropped=dropped))
    return replace(digest, sections=tuple(sections))


def _digest(data: DigestData, links: DigestLinks, targets: Targets) -> _Digest:
    soon = tuple(_card(ad, data.day, links, targets) for ad in data.apply_soon)
    fresh = tuple(_card(ad, data.day, links, targets) for ad in data.new_today)
    earlier = tuple(_card(ad, data.day, links, targets) for ad in data.carried)
    site = links.site_url
    sections = tuple(
        _Section(title, cards, 0)
        for title, cards in (
            (APPLY_SOON_TITLE, soon),
            (NEW_TODAY_TITLE, fresh),
            (CARRIED_TITLE, earlier),
        )
        if cards
    )
    hidden_text = hidden_line(data.hidden, data.max_years)
    matches = _plural(len(soon) + len(fresh) + len(earlier), "match", "matches")
    return _Digest(
        subject=f"{PRODUCT_NAME}: {matches}, {_long_date(data.day)}",
        date_line=_long_date(data.day),
        lead=_lead([*soon, *fresh], data.new_ads, data.ran_at),
        sections=sections,
        hidden_text=hidden_text,
        hidden_link=(
            _Link("view", f"{site}/app/hidden") if site and hidden_text else None
        ),
        notes=_notes(data),
        footer_links=_footer_links(site),
        dropped_note="more on the website" if site else "more in jobranger report",
    )


def _card(ad: Ad, day: date, links: DigestLinks, targets: Targets) -> _Card:
    deadline = parse_date(ad.deadline)
    return _Card(
        ad_id=ad.id,
        due=_short_date(deadline) if deadline else NO_DATE,
        due_note=_due_note(deadline, day),
        fit=display_fit(ad, targets),
        company=_truncate(ad.company, FIELD_CHAR_LIMIT),
        title=_truncate(ad.display_title or ad.title, FIELD_CHAR_LIMIT),
        meta=MIDDLE_DOT.join(
            _truncate(part, FIELD_CHAR_LIMIT)
            for part in (ad.location, role_label(ad.role_type), _source_label(ad))
            if part.strip()
        ),
        why=_truncate(ad.summary or "", SUMMARY_CHAR_LIMIT),
        links=_card_links(ad, links.site_url),
    )


def _card_links(ad: Ad, site: str | None) -> tuple[_Link, ...]:
    found: list[_Link] = []
    parts = _web_parts(ad.url)
    if parts:
        found.append(_Link(f"Open the ad {NORTH_EAST_ARROW}", urlunsplit(parts)))
    if not site:
        return tuple(found)
    ad_link = f"{site}/app?ad={quote(_replace_surrogates(ad.id), safe='')}"
    if len(ad_link) <= URL_CHAR_LIMIT:
        found.append(_Link("Mark applied", f"{ad_link}&action=applied"))
        found.append(_Link("Not for me", f"{ad_link}&action=not_for_me", muted=True))
    return tuple(found)


def _footer_links(site: str | None) -> tuple[_Link, ...]:
    if not site:
        return ()
    return (
        _Link("Open jobranger", f"{site}/app"),
        _Link("Settings", f"{site}/app/settings"),
        _Link("Unsubscribe", f"{site}/app/settings/delivery"),
    )


def _site_root(site_url: str | None) -> str | None:
    parts = _web_parts(site_url) if site_url else None
    if parts is None:
        return None
    return urlunsplit((parts.scheme, parts.netloc, parts.path.rstrip("/"), "", ""))


def _web_parts(url: str) -> SplitResult | None:
    if len(url) > URL_CHAR_LIMIT:
        return None
    try:
        parts = urlsplit(_replace_surrogates(url).strip())
        hostname = parts.hostname
    except ValueError:
        return None
    scheme = parts.scheme.lower()
    if scheme not in WEB_SCHEMES or not hostname or "@" in parts.netloc:
        return None
    if any(char.isspace() or not char.isprintable() for char in parts.netloc):
        return None
    result = SplitResult(
        scheme,
        parts.netloc,
        quote(parts.path, safe=URL_SAFE),
        quote(parts.query, safe=URL_SAFE),
        quote(parts.fragment, safe=URL_SAFE),
    )
    return None if len(urlunsplit(result)) > URL_CHAR_LIMIT else result


def _replace_surrogates(text: str) -> str:
    return text.encode("utf-8", "replace").decode("utf-8")


def _truncate(text: str, limit: int) -> str:
    flat = "".join(char for char in " ".join(text.split()) if _shown(char))
    if len(flat) <= limit:
        return flat
    return flat[: limit - 1] + ELLIPSIS


def _shown(char: str) -> bool:
    return (
        unicodedata.category(char) not in HIDDEN_CATEGORIES
        and char not in BIDI_CONTROLS
    )


def _source_label(ad: Ad) -> str:
    if is_direct_source(ad):
        return CAREER_PAGE_LABEL
    return SOURCE_LABELS.get(ad.source, ad.source)


def _source_phrase(source: str) -> str:
    if source.startswith(DIRECT_SOURCE_PREFIXES):
        key = _truncate(source.partition(":")[2], FIELD_CHAR_LIMIT)
        return f"the {key} career page"
    return _truncate(SOURCE_LABELS.get(source, source), FIELD_CHAR_LIMIT)


def _due_note(deadline: date | None, day: date) -> str:
    if deadline is None:
        return APPLY_EARLY
    days = (deadline - day).days
    if days == 0:
        return "today"
    if days == 1:
        return "tomorrow"
    return f"in {days} days"


def _lead(cards: list[_Card], new_ads: int, ran_at: datetime) -> str:
    part_of_day = _part_of_day(ran_at)
    new_ads_text = _plural(new_ads, "new ad", "new ads")
    fits = [
        _fit_phrase(sum(card.fit == fit for card in cards), fit) for fit in LEAD_FITS
    ]
    shown = [phrase for phrase in fits if phrase]
    if not shown:
        return f"No new matches this {part_of_day}, from {new_ads_text}."
    return (
        f"{_capitalize(_join_and(shown))} this {part_of_day}, "
        f"picked from {new_ads_text}."
    )


def _fit_phrase(count: int, fit: DisplayFit) -> str:
    if not count:
        return ""
    if fit == "on_request":
        return f"{_number(count)} {'ad' if count == 1 else 'ads'} shown on request"
    return f"{_number(count)} {fit} {'fit' if count == 1 else 'fits'}"


def _notes(data: DigestData) -> tuple[str, ...]:
    notes = (
        _read_note(data),
        _closed_note(data.closed_since_last),
        _failed_note(data.failed_sources),
        _paused_note(data),
        _stopped_note(data),
    )
    return tuple(note for note in notes if note)


def _read_note(data: DigestData) -> str:
    read = [
        _source_phrase(source)
        for source in data.sources_read
        if not source.startswith(DIRECT_SOURCE_PREFIXES)
    ]
    if data.employer_pages:
        read.append(
            _plural(data.employer_pages, "company career page", "company career pages")
        )
    if not read:
        return ""
    when = "overnight" if _local_hour(data.ran_at) < OVERNIGHT_END_HOUR else "today"
    return f"Read {_join_and(read)} {when}."


def _closed_note(closed: int) -> str:
    if not closed:
        return ""
    jobs = "earlier job" if closed == 1 else "earlier jobs"
    return f"{_capitalize(_number(closed))} {jobs} closed since the last run."


def _failed_note(failed: Sequence[str]) -> str:
    if not failed:
        return ""
    names = [_source_phrase(source) for source in failed]
    return f"Could not read {_join_capped(names, FAILED_SOURCES_SHOWN)}."


def _stopped_note(data: DigestData) -> str:
    if data.model_stopped is None:
        return ""
    waiting = "ad waits" if data.pending == 1 else "ads wait"
    return STOP_NOTES[data.model_stopped].format(pending=data.pending, waiting=waiting)


def _paused_note(data: DigestData) -> str:
    if not data.budget_paused:
        return ""
    waiting = "ad waits" if data.pending == 1 else "ads wait"
    return f"AI checks are paused until next month: {data.pending} {waiting}."


def _local_hour(moment: datetime) -> int:
    return moment.astimezone().hour


def _part_of_day(moment: datetime) -> str:
    hour = _local_hour(moment)
    if hour < NOON_HOUR:
        return "morning"
    if hour < EVENING_HOUR:
        return "afternoon"
    return "evening"


def _long_date(day: date) -> str:
    return f"{day:%A} {day.day} {day:%B}"


def _short_date(day: date) -> str:
    return f"{day.day} {day:%b}"


def _number(count: int) -> str:
    return NUMBER_WORDS[count] if count < len(NUMBER_WORDS) else str(count)


def _plural(count: int, singular: str, plural: str) -> str:
    return f"{count} {singular if count == 1 else plural}"


def _capitalize(text: str) -> str:
    return text[:1].upper() + text[1:]


def _join_and(items: Sequence[str]) -> str:
    if len(items) <= 1:
        return "".join(items)
    return f"{', '.join(items[:-1])} and {items[-1]}"


def _join_capped(items: Sequence[str], shown: int) -> str:
    if len(items) <= shown:
        return _join_and(items)
    return f"{', '.join(items[:shown])} and {len(items) - shown} more"


def _html(digest: _Digest, cards: Sequence[str]) -> str:
    pages = iter(cards)
    sections = (
        _html_section(
            section, "".join(islice(pages, len(section.cards))), digest.dropped_note
        )
        for section in digest.sections
    )
    body = "".join(
        [_html_header(digest), *sections, _html_hidden(digest), _html_footer(digest)]
    )
    return (
        '<!doctype html><html lang="en"><head><meta charset="utf-8">'
        '<meta name="viewport" content="width=device-width, initial-scale=1">'
        '<meta name="color-scheme" content="light only">'
        '<meta name="supported-color-schemes" content="light only">'
        f"<title>{escape(digest.subject)}</title></head>"
        f'<body style="margin:0;padding:0;background:{PAGE_BACKGROUND};">'
        f'{TABLE_OPEN} width="100%" style="background:{PAGE_BACKGROUND};"><tr>'
        '<td align="center" style="padding:36px 3%;">'
        f'{TABLE_OPEN} width="600" style="width:100%;max-width:600px;'
        f"background:{CARD_BACKGROUND};border:1px solid {CARD_BORDER};"
        f'font-family:{FONT};color:{INK};">'
        f'<tr><td style="padding:36px 6% 32px;">{body}</td></tr></table>'
        "</td></tr></table></body></html>"
    )


def _rule(color: str, gap: int) -> str:
    return (
        f'<div style="height:1px;line-height:1px;font-size:1px;background:{color};'
        f'margin-top:{gap}px;">&nbsp;</div>'
    )


def _html_header(digest: _Digest) -> str:
    return (
        f'{TABLE_OPEN} width="100%"><tr>'
        '<td style="font-size:26px;font-weight:600;letter-spacing:-0.01em;">'
        f"{PRODUCT_NAME}</td>"
        f'<td align="right" style="font-size:13px;color:{MUTED};{WRAP}">'
        f"{escape(digest.date_line)}</td></tr></table>"
        f"{_rule(INK, 12)}{_rule(INK, 3)}"
        f'<p style="margin:22px 0 0;font-size:22px;line-height:1.4;{WRAP}">'
        f"{escape(digest.lead)}</p>"
    )


def _html_section(section: _Section, cards: str, dropped_note: str) -> str:
    label = (
        f'{TABLE_OPEN} width="100%" style="margin-top:30px;"><tr>'
        '<td style="white-space:nowrap;padding-right:12px;font-size:12px;'
        'font-weight:600;letter-spacing:0.12em;text-transform:uppercase;">'
        f"{escape(section.title)}</td>"
        f'<td width="100%">{_rule(RULE, 0)}</td></tr></table>'
    )
    dropped = (
        f'<p style="margin:14px 0 0;font-size:13px;color:{MUTED};">'
        f"{section.dropped} {escape(dropped_note)}</p>"
        if section.dropped
        else ""
    )
    return label + cards + dropped


def _html_card(card: _Card) -> str:
    links = "".join(
        f'<a href="{escape(link.href)}" style="color:{MUTED if link.muted else INK};'
        f'margin-right:18px;display:inline-block;">{escape(link.label)}</a>'
        for link in card.links
    )
    link_row = (
        '<div style="margin-top:16px;font-size:14px;font-weight:500;'
        f'{WRAP}">{links}</div>'
        if links
        else ""
    )
    why = (
        f'<p style="margin:14px 0 0;font-size:17px;line-height:1.45;{WRAP}">'
        f"{escape(card.why)}</p>"
        if card.why
        else ""
    )
    return (
        f'{TABLE_OPEN} width="100%" '
        f'style="table-layout:fixed;border-bottom:1px solid {RULE};"><tr>'
        f'<td width="{DUE_COLUMN_PX + 20}" valign="top" '
        f'style="width:{DUE_COLUMN_PX}px;padding:22px 20px 22px 0;{WRAP}">'
        '<div style="font-size:22px;font-weight:600;line-height:1.1;">'
        f"{escape(card.due)}</div>"
        f'<div style="font-size:12px;color:{MUTED};margin-top:4px;">'
        f"{escape(card.due_note)}</div>"
        '<div style="margin-top:14px;font-size:12px;font-weight:600;'
        f'color:{FIT_COLORS[card.fit]};">{escape(FIT_LABELS[card.fit])}</div></td>'
        f'<td valign="top" style="padding:22px 0;{WRAP}">'
        f'<div lang="{AD_LANG}" style="font-size:13px;color:{MUTED};'
        f'{WRAP}{HYPHENATE}">{escape(card.company)}</div>'
        f'<div lang="{AD_LANG}" style="font-size:23px;font-weight:600;'
        f'line-height:1.2;margin-top:2px;{WRAP}{HYPHENATE}">{escape(card.title)}</div>'
        f'<div style="margin-top:8px;font-size:13px;color:{BODY_TEXT};{WRAP}">'
        f"{escape(card.meta)}</div>"
        f"{why}{link_row}</td></tr></table>"
    )


def _html_hidden(digest: _Digest) -> str:
    if not digest.hidden_text:
        return ""
    view = (
        f'{MIDDLE_DOT}<a href="{escape(digest.hidden_link.href)}" '
        f'style="color:{INK};">{escape(digest.hidden_link.label)}</a>'
        if digest.hidden_link
        else ""
    )
    return (
        '<p style="margin:26px 0 0;font-size:14px;line-height:1.6;'
        f'color:{BODY_TEXT};{WRAP}">'
        f"{escape(digest.hidden_text)}{view}</p>"
    )


def _html_footer(digest: _Digest) -> str:
    links = MIDDLE_DOT.join(
        f'<a href="{escape(link.href)}" style="color:{MUTED};">{escape(link.label)}</a>'
        for link in digest.footer_links
    )
    last_line = links or escape(STOP_EMAILS)
    notes = escape(" ".join(digest.notes))
    footer = f"{notes}<br>{last_line}" if notes else last_line
    return (
        f"{_rule(RULE, 28)}"
        '<p style="margin:14px 0 0;font-size:12px;line-height:1.7;'
        f'color:{MUTED};{WRAP}">'
        f"{footer}</p>"
    )


def _text(digest: _Digest) -> str:
    blocks = [f"{PRODUCT_NAME}{MIDDLE_DOT}{digest.date_line}", digest.lead]
    for section in digest.sections:
        blocks.append(section.title.upper())
        blocks.extend(_text_card(card) for card in section.cards)
        if section.dropped:
            blocks.append(f"{section.dropped} {digest.dropped_note}")
    if digest.hidden_text:
        view = (
            f"{MIDDLE_DOT}view: {digest.hidden_link.href}" if digest.hidden_link else ""
        )
        blocks.append(f"{digest.hidden_text}{view}")
    footer = list(digest.notes)
    footer.extend(f"{link.label}: {link.href}" for link in digest.footer_links)
    if not digest.footer_links:
        footer.append(STOP_EMAILS)
    blocks.append("\n".join(footer))
    return "\n\n".join(blocks) + "\n"


def _text_card(card: _Card) -> str:
    lines = [
        f"{card.due}, {card.due_note}{MIDDLE_DOT}{FIT_LABELS[card.fit]}",
        card.company,
        card.title,
        card.meta,
    ]
    if card.why:
        lines.append(card.why)
    lines.extend(f"{link.label}: {link.href}" for link in card.links)
    return "\n".join(lines)
