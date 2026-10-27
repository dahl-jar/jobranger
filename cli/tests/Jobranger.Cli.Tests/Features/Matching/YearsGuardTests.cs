using System.Text.Json;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class YearsGuardTests {
    private const string javaYears = "Minimum 3 års erfaring med Java";
    private const string guardBlocker = "Candidate does not have 3 years of experience";

    private static readonly string tablePath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Contracts", "matching", "years_cases.json");
    private static readonly Limits junior = new() { Years = ["0-1"] };
    private static readonly IReadOnlyDictionary<string, JsonElement> cases = Load();

    public static TheoryData<string> CaseIds() => [.. cases.Keys];

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void ShouldFindStatedYears_ForCase(string id) {
        JsonElement row = cases[id];
        StatedYears? expected = row.GetProperty("stated_years").ValueKind == JsonValueKind.Null
            ? null
            : new StatedYears(row.GetProperty("stated_years").GetInt32(), row.GetProperty("phrase").GetString()!);

        StatedYears? found = YearsGuard.Find($"{Title(row)}\n{Description(row) ?? string.Empty}");

        Assert.Equal(expected, found);
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void ShouldApplyGuard_ForCase(string id) {
        JsonElement row = cases[id];
        Match match = MatchingFixtures.StrongMatch with { RequiredExperienceYears = Number(row, "model_years") };
        Limits limits = new() { Years = [.. row.GetProperty("bands").EnumerateArray().Select(band => band.GetString()!)] };

        Match result = YearsGuard.Apply(match, Title(row), Description(row), limits);

        Assert.Equal(
            (row.GetProperty("status").GetString(), Number(row, "years")),
            (result.Status, result.RequiredExperienceYears));
    }

    [Fact]
    public void ShouldNamePhraseInReason_WhenGuardBlocks() {
        Match match = MatchingFixtures.StrongMatch with { Blockers = ["Mandatory Java experience missing"] };

        Match result = YearsGuard.Apply(match, "Utvikler", javaYears, junior);

        Assert.Equal(
            "The ad asks for at least 3 years of experience: \"Minimum 3 års erfaring\". "
            + "The search accepts at most 1 years.",
            result.Reason);
        Assert.Equal([guardBlocker, "Mandatory Java experience missing"], result.Blockers);
    }

    [Fact]
    public void ShouldKeepFiveBlockers_WhenGuardAddsOne() {
        Match match = MatchingFixtures.StrongMatch with { Blockers = ["b1", "b2", "b3", "b4", "b5"] };

        Match result = YearsGuard.Apply(match, "Utvikler", javaYears, junior);

        Assert.Equal([guardBlocker, "b1", "b2", "b3", "b4"], result.Blockers);
    }

    [Fact]
    public void ShouldCutReason_WhenPhraseIsLong() {
        string description = "Minimum 3 års " + new string('x', 400) + " erfaring";

        Match result = YearsGuard.Apply(MatchingFixtures.StrongMatch, "Utvikler", description, junior);

        Assert.Equal("The ad asks for at least 3 years of experience: \"Minimum 3 års…", result.Reason);
    }

    private static string Title(JsonElement row) => row.GetProperty("title").GetString()!;

    private static string? Description(JsonElement row) => row.GetProperty("description").GetString();

    private static double? Number(JsonElement row, string field) =>
        row.GetProperty(field).ValueKind == JsonValueKind.Null ? null : row.GetProperty(field).GetDouble();

    private static Dictionary<string, JsonElement> Load() {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(tablePath));
        return document.RootElement.EnumerateArray()
            .ToDictionary(row => row.GetProperty("id").GetString()!, row => row.Clone(), StringComparer.Ordinal);
    }
}
