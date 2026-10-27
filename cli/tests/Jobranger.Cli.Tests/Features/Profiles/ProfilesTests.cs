using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
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

    private const string contextPath = "/config/context.yaml";
    private const string profileName = "profile.yaml";
    private const string idSuffix = ".id";
    private const string crashOutcome = "rejected";
    private const string jsonNull = "null";

    private static readonly JsonArray profileForms = LaxGolden.Rows("profile.json");
    private static readonly Dictionary<string, ConfigProblem> pythonProblems = new(StringComparer.Ordinal) {
        ["string_type"] = ConfigProblem.NotText,
        ["float_type"] = ConfigProblem.NotNumber,
        ["float_parsing"] = ConfigProblem.NotNumber,
        ["bool_type"] = ConfigProblem.NotBoolean,
        ["bool_parsing"] = ConfigProblem.NotBoolean,
        ["greater_than_equal"] = ConfigProblem.BelowZero,
        ["missing"] = ConfigProblem.Required,
        ["extra_forbidden"] = ConfigProblem.UnknownKey,
        ["tuple_type"] = ConfigProblem.NotList,
        ["model_type"] = ConfigProblem.NotSection,
        ["string_too_long"] = ConfigProblem.TooLong,
    };

    private readonly TempDir temp = new();

    public static TheoryData<int> ProfileFormRows => LaxGolden.Indexes(profileForms);

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

    [Fact]
    public void ShouldKeepStrongFit_WhenEvidenceIsAboutMe() {
        string? status = MatchStatus("about_me");

        Assert.Equal("strong", status);
    }

    [Fact]
    public void ShouldKeepStrongFit_WhenEvidenceIsDocument() {
        string? status = MatchStatus("doc_cv_notes_md");

        Assert.Equal("strong", status);
    }

    [Fact]
    public void ShouldThrow_WhenContextIdClashesWithProfileId() {
        Profile profile = new() {
            Summary = "Developer",
            ProfessionalYears = 1,
            Projects = [new Evidence("about_me", "Java", "Java.", null, null)],
        };
        UserContext context = ProfilesEntry.WithAbout(new UserContext(), ContextFixtures.About);

        ConfigInvalidException error =
            Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.CandidateEvidence(profile, context, contextPath));

        Assert.Equal(
            (contextPath, "about_me", ConfigProblem.DuplicateKey),
            (error.Path, error.Errors[0].Location, error.Errors[0].Problem));
    }

    [Theory]
    [InlineData("    facts: Java.", "    facts: Java.\n    start: 2024-1", "projects.0.start", nameof(ConfigProblem.NotMonth))]
    [InlineData("    facts: Java.", "    facts: Java.\n    end: May 2024", "projects.0.end", nameof(ConfigProblem.NotMonth))]
    [InlineData("id: java_project", "id: education", "projects.0.id", nameof(ConfigProblem.DuplicateKey))]
    [InlineData("id: java_project", "id: about_me", "projects.0.id", nameof(ConfigProblem.ReservedId))]
    [InlineData("id: java_project", "id: doc_cv_pdf", "projects.0.id", nameof(ConfigProblem.ReservedId))]
    public void ShouldRejectProfile_WhenFieldBreaksRule(
        string field, string replacement, string location, string problem) {
        string path = Write(valid.Replace(field, replacement, StringComparison.Ordinal));

        ConfigInvalidException error =
            Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.Load(path));

        Assert.Equal(
            (path, location, Enum.Parse<ConfigProblem>(problem)),
            (error.Path, error.Errors[0].Location, error.Errors[0].Problem));
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
    public void ShouldReadNoMaster_WhenHasMasterIsAbsent() {
        string path = Write(valid);

        Profile profile = ProfilesEntry.Load(path);

        Assert.False(profile.HasMaster);
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

    [Theory]
    [MemberData(nameof(ProfileFormRows))]
    public void ShouldMatchPython_WhenProfileFieldUsesYamlForm(int row) {
        JsonNode golden = profileForms[row]!;
        string id = golden["id"]!.GetValue<string>();
        string field = golden["field"]!.GetValue<string>();
        JsonObject expect = golden["expect"]!.AsObject();
        string path = Write(golden["yaml"]!.GetValue<string>());

        string outcome = Outcome(path, field, expect.ContainsKey("crash"));

        Assert.Equal((id, Expected(id, field, expect)), (id, outcome));
    }

    [Theory]
    [InlineData("summary: s\nprofessional_years: 1\nhas_master: !!bool 1\n", "has_master", nameof(ConfigProblem.NotBoolean))]
    [InlineData("summary: s\nprofessional_years: !!int abc\n", "professional_years", nameof(ConfigProblem.NotNumber))]
    [InlineData("summary: s\nprofessional_years: 2024-02-30\n", "professional_years", nameof(ConfigProblem.NotNumber))]
    [InlineData("summary: s\nprofessional_years: !!int ' 5'\n", "professional_years", nameof(ConfigProblem.NotNumber))]
    [InlineData("summary: !!binary aGk=\nprofessional_years: 1\n", "summary", nameof(ConfigProblem.NotText))]
    public void ShouldReportFieldTypeError_WhenTagOrDateCannotBuildValue(string yaml, string location, string problem) {
        string path = Write(yaml);

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.Load(path));

        Assert.Equal(
            [(location, Enum.Parse<ConfigProblem>(problem))],
            error.Errors.Select(item => (item.Location, item.Problem)));
    }

    [Theory]
    [InlineData("summary: s\rprofessional_years: 3\t\r", "2")]
    [InlineData("professional_years: 1\nsummary: \"a\nb\"\n", "2")]
    [InlineData("summary: s\nprofessional_years: 1\nstack: [a\u0007]\n", "3")]
    public void ShouldNameLine_WhenProfileIsNotYaml(string yaml, string line) {
        string path = Write(yaml);

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => ProfilesEntry.Load(path));

        Assert.Equal(
            [(profileName, ConfigProblem.NotYaml, line)],
            error.Errors.Select(item => (item.Location, item.Problem, item.Detail)));
    }

    [Fact]
    public void ShouldWriteNullYears_WhenYearsAreInfinite() {
        string path = Write(valid.Replace("professional_years: 1", "professional_years: .inf", StringComparison.Ordinal));
        Profile profile = ProfilesEntry.Load(path);

        string text = ProfilesEntry.Text(profile);

        Assert.Contains("\"professional_years\": null", text, StringComparison.Ordinal);
    }

    private static string? MatchStatus(string evidenceId) {
        Profile profile = ProfilesEntry.Load(MatchingFixtures.TestData("profile_user.yaml"));
        UserContext context = ProfilesEntry.WithDoc(
            ProfilesEntry.WithAbout(new UserContext(), ContextFixtures.About),
            ContextFixtures.Doc.Name,
            ContextFixtures.Doc.Text,
            ContextFixtures.Today);
        JsonObject payload = MatchingFixtures.Payload();
        payload["match"]!["evidence"] = new JsonArray(evidenceId);
        Classification? result = MatchingEntry.ToClassification(
            MatchingEntry.ParseAssessment(payload.ToJsonString())!,
            MatchingFixtures.Ad,
            ProfilesEntry.CandidateEvidence(profile, context, contextPath),
            ProfilesEntry.EducationIds(profile),
            MatchingFixtures.Narrow,
            MatchingFixtures.Version);
        return result?.Match?.Status;
    }

    private static string Expected(string id, string field, JsonObject expect) {
        if (expect.ContainsKey("crash")) {
            return crashOutcome;
        }
        if (expect["errors"] is JsonArray errors) {
            return Errors(errors.Select(item => PythonError(
                id, item!["loc"]!.GetValue<string>(), item["type"]!.GetValue<string>())));
        }
        JsonNode? value = expect["value"];
        return field switch {
            "document" => string.Empty,
            "professional_years" => LaxGolden.ReprBits(value!.GetValue<string>()).ToString()!,
            _ => value?.ToJsonString() ?? jsonNull,
        };
    }

    private static (string Location, ConfigProblem Problem) PythonError(string id, string location, string type) {
        ConfigProblem problem = type switch {
            "string_pattern_mismatch" when location.EndsWith(idSuffix, StringComparison.Ordinal) =>
                ConfigProblem.NotEvidenceId,
            "string_pattern_mismatch" => ConfigProblem.NotMonth,
            _ when pythonProblems.TryGetValue(type, out ConfigProblem mapped) => mapped,
            _ => throw new InvalidOperationException(id + ": no C# problem for Python error type " + type),
        };
        return (location.Length == 0 ? profileName : location, problem);
    }

    private static string Outcome(string path, string field, bool rejectedOnly) {
        Profile profile;
        try {
            profile = ProfilesEntry.Load(path);
        } catch (ConfigInvalidException error) {
            return rejectedOnly ? crashOutcome : Errors(error.Errors.Select(item => (item.Location, item.Problem)));
        }
        Evidence? evidence = profile.Projects.Count > 0 ? profile.Projects[0] : null;
        return field switch {
            "document" => string.Empty,
            "professional_years" => LaxGolden.Bits(profile.ProfessionalYears).ToString()!,
            "summary" => Json(profile.Summary),
            "has_master" => JsonValue.Create(profile.HasMaster).ToJsonString(),
            "languages" => Texts(profile.Languages),
            "stack" => Texts(profile.Stack),
            "evidence.id" => Json(evidence?.Id),
            "evidence.name" => Json(evidence?.Name),
            "evidence.facts" => Json(evidence?.Facts),
            "evidence.start" => Json(evidence?.Start),
            "evidence.end" => Json(evidence?.End),
            _ => throw new InvalidOperationException("no profile field " + field),
        };
    }

    private static string Json(string? text) => JsonValue.Create(text)?.ToJsonString() ?? jsonNull;

    private static string Texts(IReadOnlyList<string> items) =>
        new JsonArray([.. items.Select(item => (JsonNode?)JsonValue.Create(item))]).ToJsonString();

    private static string Errors(IEnumerable<(string Location, ConfigProblem Problem)> errors) =>
        string.Join("; ", errors.Select(error => error.Location + " " + error.Problem).Order(StringComparer.Ordinal));

    private string Write(string text) {
        string path = temp.File("profile.yaml");
        File.WriteAllText(path, text);
        return path;
    }
}
