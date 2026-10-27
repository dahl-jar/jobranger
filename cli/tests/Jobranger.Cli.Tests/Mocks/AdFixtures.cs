using System.Globalization;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Mocks;

internal static class AdFixtures {
    public const string CurrentVersion = "test-version";
    public const string OutdatedVersion = "old-version";
    public const string Assessment = "{\"status\": \"strong\", \"version\": \"test-version\"}";
    public const string GeneralSoftware = "general_software";

    public static readonly DateTimeOffset Now = new(2026, 10, 9, 10, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset Later = Now.AddDays(1);
    public static readonly DateOnly Today = DateOnly.FromDateTime(Now.UtcDateTime);

    public static readonly Ad Primary = new() {
        Id = "finn:1234",
        Source = "finn",
        SourceId = "1234",
        Title = "Utvikler",
        Company = "Org One AS",
        Location = "Bergen",
        Published = "2026-08-24T00:00:00+00:00",
        Url = "https://jobs.example.test/ad/1234",
        IsProgrammingJob = true,
        DisplayTitle = "Software Developer",
        RoleType = GeneralSoftware,
        Assessment = Assessment,
        AssessmentVersion = CurrentVersion,
    };

    public static readonly StoredClassification Classification = new(
        "junior",
        "Build Java services.",
        RequiresMaster: false,
        IsProgrammingJob: true,
        "Backend Developer",
        "backend",
        ["Java", "Søk"],
        ["Bachelor's degree"],
        Assessment);

    public static readonly Targets AllTargets = new() { Subfields = Targets.AllRoles };

    public static Ad Numbered(int index) {
        string sourceId = index.ToString(CultureInfo.InvariantCulture);
        return Primary with { Id = "finn:" + sourceId, SourceId = sourceId };
    }

    public static FakeTimeProvider Clock() => new(Now);
}
