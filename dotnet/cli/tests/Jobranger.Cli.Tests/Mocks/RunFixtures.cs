using System.Collections;
using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Catalogue;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Features.Sources;
using Jobranger.Cli.Files;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Mocks;

internal static class RunFixtures {
    public const string Description = "Build Java services for a public client.";
    public const string ApplyUrl = "https://apply.example.test/jobs/1";

    public static readonly EmployerFeed Feed =
        new("teamtailor", "example-tech", "Example Tech AS", "https://careers.example.test/jobs.rss");
    public static readonly string JavaReply = MatchingCases.Replies()["java_project"];

    public static Paths TempPaths(TempDir dir) => new(new Hashtable {
        ["JOBRANGER_CONFIG_DIR"] = Path.Combine(dir.Path, "config"),
        ["JOBRANGER_DATA_DIR"] = Path.Combine(dir.Path, "data"),
        ["JOBRANGER_STATE_DIR"] = Path.Combine(dir.Path, "state"),
    });

    public static AppConfig Config(string profilePath, SourcesConfig? sources = null) => new() {
        Profile = new ProfileRef(profilePath),
        Targets = MatchingFixtures.AllRoles(),
        Sources = sources ?? new SourcesConfig { Nav = false, Employers = false },
        Model = new ModelConfig { Provider = ProviderName.Anthropic, Model = "claude-haiku-4-5" },
    };

    [UnsupportedOSPlatform("windows")]
    public static Runs Create(
        TestDatabase db,
        Paths paths,
        HttpClient http,
        IModelClient? client,
        IReadOnlyList<ISource> sources,
        FakeTimeProvider time) => new(
            db.Connection,
            paths,
            http,
            (_, _, _) => client is null ? throw new ModelSetupException("no model chosen") : Task.FromResult(client),
            _ => sources,
            time,
            NullLogger.Instance);

    public static SourceBatch Batch(string source, params SourceListing[] listings) => new() {
        Source = source,
        Listings = listings,
        SeenIds = new HashSet<string>(listings.Select(listing => listing.Id), StringComparer.Ordinal),
    };

    public static Ad Pending(int index, string title, string company) {
        string sourceId = index.ToString(CultureInfo.InvariantCulture);
        return new Ad {
            Id = "finn:" + sourceId,
            Source = "finn",
            SourceId = sourceId,
            Title = title,
            Company = company,
            Location = "Bergen",
            Published = "2026-10-01T00:00:00+00:00",
            Url = SourceFixtures.FinnAdBase + sourceId,
            Description = Description,
        };
    }
}
