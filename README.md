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

MIT, see [LICENSE](LICENSE).
