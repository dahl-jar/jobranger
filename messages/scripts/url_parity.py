from __future__ import annotations

import ipaddress
import json
import sys
from pathlib import Path
from urllib.parse import quote, urlsplit

OUTPUT = Path("messages/fixtures/urls") / "parity.json"
PRINTABLE = "".join(chr(code) for code in range(0x21, 0x7F))
ACE_CODEC = "idna"
BRACKET_OPEN = "["
BRACKET_CLOSE = "]"

URLS = [
    "https://jobs.example.test/",
    "https://JOBS.Example.TEST/a",
    "http://jobs.example.test:8080/a?q=1#f",
    "https://ÆØÅ.example.test/",
    "https://BÅT.example.test/",
    "https://ba\u030at.example.test/",
    "https://\u00ea\u0323.example.test/",
    "https://jobs.example.te\u0301/",
    "https://jo\u00adbs.example.test/",
    "https://ｊｏｂｓ.example.test/",
    "https://jobs。example。test/",
    "http://192.168.1.1/",
    "http://127.0.0.1:8080/",
    "http://[::1]/",
    "http://[2001:DB8:0:0:1:0:0:1]/",
    "http://[2001:db8::]:8080/",
    "https://xn--bcher-kva.example.test/",
    "https://XN--BCHER-KVA.example.test/",
    "https://xn--wgv71a119e.example.test/",
    "https://bücher.example.test/",
    "https://日本語.example.test/",
    "https://한국.example.test/",
    "https://\u1100\u1162.example.test/",
    "https://\u1100\u1176.example.test/",
    "https://\u1100\u1161\u11a8.example.test/",
    "https://\uac00a.example.test/",
    "https://\uac00\u11a7.example.test/",
    "https://\uac00\u11c3.example.test/",
    "https://\u1113\u1162.example.test/",
    "https://d\u11a8.example.test/",
    "https://\uac01\u11a8.example.test/",
    "https://אa.example.test/",
]

DROPPED = {
    "https://faß.example.test/": "IDNA 2003 maps ß to ss, WHATWG keeps ß (nontransitional)",
    "https://ς.example.test/": "IDNA 2003 maps final sigma to σ, WHATWG keeps ς (nontransitional)",
    "https://a\u200db.example.test/": "IDNA 2003 removes ZWJ, WHATWG rejects it (CheckJoiners)",
    "https://xn--a.example.test/": "Python passes ACE labels through unchecked, WHATWG rejects bad punycode",
    "https://ꭰ.example.test/": "Python's IDNA 2003 tables (Unicode 3.2) lack U+AB70, WHATWG maps it to U+13A0",
    "http://127.1/": "Python's ipaddress has no IPv4 shorthand, WHATWG reads it as 127.0.0.1",
    "http://[::ffff:127.0.0.1]/": "Python prints IPv4-mapped addresses dotted, WHATWG prints ::ffff:7f00:1",
}


def host(text: str) -> str:
    if text.startswith(BRACKET_OPEN):
        return BRACKET_OPEN + ipaddress.IPv6Address(text[1:-1]).compressed + BRACKET_CLOSE
    try:
        return str(ipaddress.IPv4Address(text))
    except ipaddress.AddressValueError:
        return text.encode(ACE_CODEC).decode("ascii").lower()


def authority(netloc: str) -> tuple[str, str, str]:
    userinfo, at, rest = netloc.rpartition("@")
    if rest.startswith(BRACKET_OPEN):
        end = rest.index(BRACKET_CLOSE) + 1
        name, port = rest[:end], rest[end + 1 :]
    else:
        name, _, port = rest.partition(":")
    return userinfo + at, name, f":{int(port)}" if port else ""


def encode(url: str) -> str | None:
    parts = urlsplit(url)
    userinfo, name, port = authority(parts.netloc)
    try:
        encoded_host = host(name)
    except UnicodeError:
        return None
    text = f"{parts.scheme}://{userinfo}{encoded_host}{port}{quote(parts.path, safe=PRINTABLE)}"
    if parts.query:
        text += "?" + quote(parts.query, safe=PRINTABLE)
    if parts.fragment:
        text += "#" + quote(parts.fragment, safe=PRINTABLE)
    return text


def main() -> int:
    document = {
        "generator": "messages/scripts/url_parity.py",
        "rule": "Python urlsplit, ipaddress and the idna codec, ASCII lowercased. Null means rejected",
        "dropped": [{"url": url, "reason": reason} for url, reason in DROPPED.items()],
        "rows": [{"url": url, "encoded": encode(url)} for url in URLS],
    }
    OUTPUT.write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
