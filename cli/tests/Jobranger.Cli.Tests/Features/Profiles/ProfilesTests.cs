using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Tests.Mocks;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Tests.Features.Profiles;

public sealed class ProfilesTests : IDisposable {
    private const string valid = """
        summary: Developer
        professional_years: 1
        education:
          - id: education
            name: Degree
            facts: Bachelor.
        projects:
          - id: java_project
            name: Java
            facts: Java.
        """;

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldEqualGolden_WhenProfileHasDatesAndAccents() {
        Profile profile = ProfilesEntry.Load(MatchingFixtures.TestData("profile_dated.yaml"));

        string text = ProfilesEntry.Text(profile);

        Assert.Equal(MatchingFixtures.GoldenText("Profiles", "profile-dated.txt"), text);
    }

    [Fact]
    public void ShouldListEvidenceIdsFromEveryList() {
        Profile profile = ProfilesEntry.Load(MatchingFixtures.TestData("profile_user.yaml"));

        IReadOnlySet<string> ids = ProfilesEntry.EvidenceIds(profile);

        Assert.Equal(MatchingFixtures.EvidenceIds.Order(StringComparer.Ordinal), ids.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ShouldListEducationIds_WhenProfileHasProjects() {
        Profile profile = ProfilesEntry.Load(MatchingFixtures.TestData("profile_user.yaml"));

        IReadOnlySet<string> ids = ProfilesEntry.EducationIds(profile);

        Assert.Equal(MatchingFixtures.EducationIds, ids);
    }

    [Theory]
    [InlineData("id: java_project", "id: Java", "projects.0.id", nameof(ConfigProblem.NotEvidenceId))]
    [InlineData("id: java_project", "id: j", "projects.0.id", nameof(ConfigProblem.NotEvidenceId))]
    [InlineData("    facts: Java.", "    facts: Java.\n    start: 2024-1", "projects.0.start", nameof(ConfigProblem.NotMonth))]
    [InlineData("    facts: Java.", "    facts: Java.\n    end: May 2024", "projects.0.end", nameof(ConfigProblem.NotMonth))]
    [InlineData("id: java_project", "id: education", "projects.0.id", nameof(ConfigProblem.DuplicateKey))]
    [InlineData("summary: Developer", "has_master: false", "summary", nameof(ConfigProblem.Required))]
    [InlineData("summary: Developer", "summary: Developer\nskills: []", "skills", nameof(ConfigProblem.UnknownKey))]
    public void ShouldRejectProfile_WhenFieldBreaksRule(
        string field, string replacement, string location, string problem) {
        string path = Write(valid.Replace(field, replacement, StringComparison.Ordinal));

        ConfigInvalidException error =
            Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.Load(path));

        Assert.Equal(
            (path, location, Enum.Parse<ConfigProblem>(problem)),
            (error.Path, error.Errors[0].Location, error.Errors[0].Problem));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, true)]
    public void ShouldRejectYears_WhenBelowZero(int years, bool rejected) {
        string path = Write(valid.Replace("professional_years: 1", $"professional_years: {years}", StringComparison.Ordinal));

        Exception? error = Record.Exception(() => ProfilesEntry.Load(path));

        Assert.Equal(rejected, error is ConfigInvalidException { Errors: [{ Problem: ConfigProblem.BelowZero }] });
    }

    [Fact]
    public void ShouldRejectProfile_WhenRootIsList() {
        string path = Write("- a\n");

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.Load(path));

        Assert.Equal(ConfigProblem.NotSection, error.Errors.Single().Problem);
    }

    [Fact]
    public void ShouldReportOnlyRequired_WhenEvidenceIdsAreMissing() {
        string path = Write(valid.Replace(
            "  - id: java_project\n    name: Java\n    facts: Java.",
            "  - name: Java\n    facts: Java.\n  - name: Go\n    facts: Go.",
            StringComparison.Ordinal));

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.Load(path));

        Assert.Equal(
            [("projects.0.id", ConfigProblem.Required), ("projects.1.id", ConfigProblem.Required)],
            error.Errors.Select(item => (item.Location, item.Problem)));
    }

    [Fact]
    public void ShouldRejectProfile_WhenPathIsDirectory() {
        string path = temp.File("profile.yaml");
        Directory.CreateDirectory(path);

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.Load(path));

        Assert.Equal((path, ConfigProblem.Unreadable), (error.Path, error.Errors[0].Problem));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void ShouldRejectProfile_WhenFileIsUnreadable() {
        string path = Write(valid);
        File.SetUnixFileMode(path, UnixFileMode.None);

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.Load(path));

        Assert.Equal((path, ConfigProblem.Unreadable), (error.Path, error.Errors[0].Problem));
    }

    [Theory]
    [InlineData(1200, false)]
    [InlineData(1201, true)]
    public void ShouldRejectFacts_WhenOverLimit(int length, bool rejected) {
        string path = Write(valid.Replace("facts: Java.", "facts: " + new string('x', length), StringComparison.Ordinal));

        Exception? error = Record.Exception(() => ProfilesEntry.Load(path));

        Assert.Equal(rejected, error is ConfigInvalidException { Errors: [{ Problem: ConfigProblem.TooLong }] });
    }

    private string Write(string text) {
        string path = temp.File("profile.yaml");
        File.WriteAllText(path, text);
        return path;
    }
}
