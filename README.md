# jobranger

jobranger fetches job ads from FINN, NAV, LinkedIn and employer job boards, checks each one against a candidate profile with an AI model, and reports the ads that fit.

| Folder | Contents |
|---|---|
| `cli/` | C# CLI, one self-contained binary for macOS and Linux |
| `cli-python/` | Python CLI that fetches, matches and reports ads |
| `relay/` | ASP.NET Core relay between the CLI and the website |
| `edge/` | Cloudflare Worker that rate limits requests to the relay by address and network |
| `web/` | React website |
| `contracts/` | Message schemas and the encrypted session layout (`contracts/session/v1.md`) shared by the CLI and the website, plus the C# contracts library (`contracts/src/Jobranger.Contracts`) with the envelope rules, payload records and payload codec |
| `scripts/` | `codegen/postprocess.py`, the last step of `contracts/generate.sh`, and `unicode/idna_hosts.py`, which writes the host table in `contracts/unicode` |

Development setup and checks are in [CONTRIBUTING.md](CONTRIBUTING.md).

## CLI files

The C# CLI keeps `config.yaml` and `profile.yaml` in `~/.config/jobranger`. The database goes in `~/Library/Application Support/jobranger` on macOS and `~/.local/share/jobranger` on Linux, and the run lock and logs in `~/Library/Application Support/jobranger` on macOS and `~/.local/state/jobranger` on Linux. `JOBRANGER_CONFIG_DIR`, `JOBRANGER_DATA_DIR` and `JOBRANGER_STATE_DIR` set each folder directly. Next come absolute `XDG_CONFIG_HOME`, `XDG_DATA_HOME` and `XDG_STATE_HOME`, each with `jobranger` appended.

Files are written with mode 0600 in 0700 folders, and a folder open to group or others is set to 0700 with a warning in the log. Saving `config.yaml` keeps comments with their keys, the file's key order and the header comment on top. Blank lines and comments inside lists are dropped. A config error names the setting and the problem, for example `targets.limits.hide_masters: is not true or false`, and never the value. `language: en` or `language: nb` picks the CLI language. Without it, `LC_ALL` or `LANG` decides.

## Website

`/` is the landing page with install steps and an example match. `/setup` walks through install, `jobranger init`, pairing and scheduling with example terminal output, one step per `?step=1` to `?step=8`.

The site uses JetBrainsMono Nerd Font, served from `web/public/fonts` (SIL OFL 1.1, license next to the files). The theme switch offers Light, System and Dark, with Dark as the default. The choice is stored in the browser under `jobranger.theme`, and a script in the page head applies it before the first paint.

The site is in English and Norwegian bokmål. It starts in the browser language (`nb`, `no` and `nn` give bokmål, anything else English) and switches in Settings › Appearance. The choice is stored under `jobranger.language`, and a script in the page head sets `<html lang>` before the first paint.

After pairing, the browser connects to the machine running jobranger through the relay, and every message between them is end-to-end encrypted. When the relay, the network or the machine can't be reached, the page says which one and keeps what it already loaded read-only. The pairing keys are stored in IndexedDB (`jobranger`, store `pairing`), and the last time the machine was heard from under `jobranger.lastSeen`. "Pair a different machine" deletes both from this browser.

## Edge

The Worker in `edge/` runs on `relay.jobranger.no/*` in front of the Cloudflare Tunnel. It counts each request against two keys from `CF-Connecting-IP`: the address (IPv4 address, IPv6 /64) and its network (IPv4 /24, IPv6 /48). Over either limit it answers 429 with `Retry-After: 60` and the relay's `rate_limited` problem body, and the request stops at the edge. Under both it passes the request to the tunnel, WebSocket upgrades included, with `X-Real-IP` set to the `CF-Connecting-IP` address and `X-Forwarded-For` removed. On a same-zone Worker subrequest Cloudflare sets the origin's `CF-Connecting-IP` from `X-Real-IP`, so the relay keys on the address Cloudflare saw. Paths outside `/pair`, `/session` and `/hub` get 404, after dot segments are resolved.

Limits per Cloudflare location, per 60 seconds:

| Paths | Per address | Per network | Relay limit it stays under |
|---|---|---|---|
| `/pair`, `/pair/*` | 6 | 30 | `/pair/claim`: burst 5, then 1 every 30 s |
| `/session`, `/session/*` | 30 | 150 | 30 per paired channel per 10 minutes |
| `/hub`, `/hub/*` | 10 | 16 | 10 open sockets per address, 16 per network |

The network limit is only charged when the address is under its own limit. CORS preflights have their own count with the same limits. A failing rate limit binding lets the request through, and the relay applies its own limits. Logs carry the path group and the tier that refused, with no address.

The account's Workers Free plan allows 100,000 Worker requests a day, shared by every Worker on the account and reset at midnight UTC. Refused requests count too. WAF rules run before the Worker, and the per-IP rate limiting rule allows one address 5 requests per 10 seconds on the relay paths, so no single address can spend the quota. The route is set to fail open, so past the quota requests skip the Worker and reach the tunnel with the WAF rules and the relay's own limits in front of them.

## License

[PolyForm Noncommercial 1.0.0](LICENSE). Anyone can run, change and share jobranger for free for personal and other noncommercial use, including using it in their own job search. Selling it, or running it as a paid service, is not allowed.