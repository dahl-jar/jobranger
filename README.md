# jobranger

jobranger fetches job ads from FINN, NAV, LinkedIn and employer job boards, checks each one against a candidate profile with an AI model, and reports the ads that fit.

| Folder | Contents |
|---|---|
| `cli/` | Python CLI that fetches, matches and reports ads |
| `relay/` | ASP.NET Core relay between the CLI and the website |
| `web/` | React website |
| `contracts/` | Message schemas shared by the CLI and the website |

Development setup and checks are in [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[PolyForm Noncommercial 1.0.0](LICENSE). Anyone can run, change and share jobranger for free for personal and other noncommercial use, including using it in their own job search. Selling it, or running it as a paid service, is not allowed.