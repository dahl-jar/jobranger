# jobranger

jobranger fetches job ads from FINN, NAV, LinkedIn and employer job boards, checks each one against a candidate profile with an AI model, and reports the ads that fit.

| Folder | Contents |
|---|---|
| `cli/` | Python CLI that fetches, matches and reports ads |
| `relay/` | ASP.NET Core relay between the CLI and the website |
| `web/` | React website |
| `contracts/` | Message schemas and the encrypted session layout (`contracts/session/v1.md`) shared by the CLI and the website |

Development setup and checks are in [CONTRIBUTING.md](CONTRIBUTING.md).

## Website

`/` is the landing page with install steps and an example match. `/setup` walks through install, `jobranger init`, pairing and scheduling with example terminal output, one step per `?step=1` to `?step=8`.

The site uses JetBrainsMono Nerd Font, served from `web/public/fonts` (SIL OFL 1.1, license next to the files). The theme switch offers Light, System and Dark, with Dark as the default. The choice is stored in the browser under `jobranger.theme`, and a script in the page head applies it before the first paint.

The site is in English and Norwegian bokmål. It starts in the browser language (`nb`, `no` and `nn` give bokmål, anything else English) and switches in Settings › Appearance. The choice is stored under `jobranger.language`, and a script in the page head sets `<html lang>` before the first paint.

After pairing, the browser connects to the machine running jobranger through the relay, and every message between them is end-to-end encrypted. When the relay, the network or the machine can't be reached, the page says which one and keeps what it already loaded read-only. The pairing keys are stored in IndexedDB (`jobranger`, store `pairing`), and the last time the machine was heard from under `jobranger.lastSeen`. "Pair a different machine" deletes both from this browser.

## License

[PolyForm Noncommercial 1.0.0](LICENSE). Anyone can run, change and share jobranger for free for personal and other noncommercial use, including using it in their own job search. Selling it, or running it as a paid service, is not allowed.