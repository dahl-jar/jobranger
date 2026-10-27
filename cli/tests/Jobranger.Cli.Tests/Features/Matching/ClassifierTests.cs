using System.Text.Json;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Profiles;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class ClassifierTests {
    private const string ellipsis = "…";

    public static TheoryData<string> CaseIds() => [.. MatchingCases.All().Keys];

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void ShouldEqualGolden_WhenClassifyingMatchingCase(string caseId) {
        Profile profile = ProfilesEntry.Load(MatchingFixtures.TestData("profile_user.yaml"));
        AdAssessment assessment = MatchingEntry.ParseAssessment(MatchingCases.Replies()[caseId])!;
        using JsonDocument golden = JsonDocument.Parse(MatchingFixtures.GoldenText("Classify", "classifications.json"));
        JsonElement expected = golden.RootElement.GetProperty(caseId);

        Classification? result = MatchingEntry.ToClassification(
            assessment,
            MatchingCases.Ad(MatchingCases.All()[caseId]),
            ProfilesEntry.EvidenceIds(profile),
            ProfilesEntry.EducationIds(profile),
            MatchingFixtures.Narrow,
            MatchingFixtures.Version);

        Assert.Equal(GoldenJson.Canonical(expected), result is null ? "null" : Canonical(result));
    }

    [Fact]
    public void ShouldDropMatch_WhenEvidenceIdIsUnknown() {
        Classification? result = Classify(match => match["evidence"] = new JsonArray("sap_certification"));

        Assert.Null(result);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("gaps")]
    public void ShouldCutReasonAtWord_WhenLongerThan300Chars(string field) {
        string text = string.Concat(Enumerable.Repeat("word ", 80));

        Classification? result = Classify(match => {
            match["status"] = "stretch";
            match[field] = field == "reason" ? text : new JsonArray(text);
        });

        string cut = string.Join(' ', Enumerable.Repeat("word", 59)) + ellipsis;
        Assert.Equal(cut, field == "reason" ? result?.Match?.Reason : result?.Match?.Gaps.Single());
    }

    [Theory]
    [InlineData("""{"blockers": ["Mandatory SAP experience not documented"]}""")]
    [InlineData("""{"is_low_code": true}""")]
    [InlineData("""{"required_experience_years": 1.5}""")]
    public void ShouldDemoteToUnsuitable_WhenMatchHasHardMismatch(string overrides) {
        Classification? result = Classify(match => MatchingFixtures.Merge(match, overrides));

        Assert.Equal("unsuitable", result?.Match?.Status);
    }

    [Fact]
    public void ShouldKeepStrong_WhenYearsEqualLimit() {
        Classification? result = Classify(match => match["required_experience_years"] = 1);

        Assert.Equal("strong", result?.Match?.Status);
    }

    [Theory]
    [InlineData("""{"evidence": []}""")]
    [InlineData("""{"evidence": ["education"]}""")]
    [InlineData("""{"reason": " "}""")]
    [InlineData("""{"required_experience_years": -1}""")]
    [InlineData("""{"required_experience_years": "inf"}""")]
    [InlineData("""{"required_experience_years": "nan"}""")]
    public void ShouldDropMatch_WhenFieldIsUnusable(string overrides) {
        Classification? result = Classify(match => MatchingFixtures.Merge(match, overrides));

        Assert.Null(result);
    }

    [Fact]
    public void ShouldCollapseReason_WhenItEndsWithSpaces() {
        Classification? result = Classify(match => match["reason"] = "Fits well.  ");

        Assert.Equal("Fits well.", result?.Match?.Reason);
    }

    [Fact]
    public void ShouldDropBlankItems_WhenListHasWhitespace() {
        Classification? result = Classify(match => {
            match["status"] = "stretch";
            match["gaps"] = new JsonArray(" ", "Real item");
        });

        Assert.Equal(["Real item"], result?.Match?.Gaps);
    }

    [Fact]
    public void ShouldKeepFirstFiveItems_WhenListIsLonger() {
        string[] gaps = [.. Enumerable.Range(0, 6).Select(index => $"Gap {index}")];

        Classification? result = Classify(match => {
            match["status"] = "stretch";
            match["gaps"] = new JsonArray([.. gaps.Select(gap => (JsonNode?)gap)]);
        });

        Assert.Equal(gaps[..5], result?.Match?.Gaps);
    }

    [Fact]
    public void ShouldDemoteStrongToStretch_WhenGapsExist() {
        Classification? result = Classify(match => match["gaps"] = new JsonArray("Norwegian needs confirmation"));

        Assert.Equal("stretch", result?.Match?.Status);
    }

    [Fact]
    public void ShouldForceUnsuitable_WhenReplyFlagsMaster() {
        Classification? result = Classify(_ => { }, payload => payload["requires_master"] = true);

        Assert.Equal(("unsuitable", true), (result?.Match?.Status, result?.RequiresMaster));
    }

    [Theory]
    [InlineData("Build services. Requires a master’s degree in computer science.", true)]
    [InlineData("Build services with a relevant master's degree.", true)]
    [InlineData("Build services. Requires a master's degree or a bachelor's degree.", false)]
    [InlineData("Build services. A master's degree is preferred.", false)]
    public void ShouldReadMasterFlag_WhenSummaryNamesDegree(string summary, bool required) {
        Classification? result = Classify(_ => { }, payload => payload["summary"] = summary);

        Assert.Equal(required, result?.RequiresMaster);
    }

    [Fact]
    public void ShouldCutSummary_WhenLongerThan700Chars() {
        Classification? result = Classify(_ => { }, payload => payload["summary"] = new string('x', 701));

        Assert.Equal(new string('x', 700), result?.Summary);
    }

    [Fact]
    public void ShouldStoreNoSummary_WhenSummaryIsEmpty() {
        Classification? result = Classify(_ => { }, payload => payload["summary"] = string.Empty);

        Assert.Equal((null, false, "strong"), (result?.Summary, result?.RequiresMaster, result?.Match?.Status));
    }

    [Theory]
    [InlineData("display_title", " ")]
    [InlineData("display_title", null)]
    [InlineData("role_type", null)]
    public void ShouldDropClassification_WhenProgrammingJobLacksRole(string field, string? value) {
        Classification? result = Classify(_ => { }, payload => payload[field] = value);

        Assert.Null(result);
    }

    [Fact]
    public void ShouldCutDisplayTitle_WhenLongerThan80Chars() {
        Classification? result = Classify(_ => { }, payload => payload["display_title"] = new string('t', 81));

        Assert.Equal(new string('t', 80), result?.DisplayTitle);
    }

    [Fact]
    public void ShouldClearRoleFields_WhenNotProgrammingJob() {
        Classification? result = Classify(_ => { }, payload => payload["is_programming_job"] = false);

        Assert.Equal((null, null), (result?.DisplayTitle, result?.RoleType));
    }

    [Theory]
    [InlineData("technologies", 5)]
    [InlineData("requirements", 3)]
    public void ShouldKeepFirstItems_WhenModelListsTooMany(string field, int kept) {
        Classification? result = Classify(
            _ => { }, payload => payload[field] = new JsonArray([.. Enumerable.Range(0, 7).Select(index => (JsonNode?)$"Item {index}")]));

        Assert.Equal(kept, (field == "technologies" ? result?.Technologies : result?.Requirements)?.Count);
    }

    [Theory]
    [InlineData("technologies", 60)]
    [InlineData("requirements", 160)]
    public void ShouldCutItemAtWord_WhenOverItemLimit(string field, int limit) {
        string text = string.Concat(Enumerable.Repeat("word ", 40));

        Classification? result = Classify(_ => { }, payload => payload[field] = new JsonArray(text));

        string item = (field == "technologies" ? result?.Technologies : result?.Requirements)![0];
        Assert.Equal((true, true), (item.EndsWith(ellipsis, StringComparison.Ordinal), item.Length <= limit));
    }

    [Fact]
    public void ShouldDropBlankTechnologies_WhenModelSendsWhitespace() {
        Classification? result = Classify(_ => { }, payload => payload["technologies"] = new JsonArray(" ", "Java"));

        Assert.Equal(["Java"], result?.Technologies);
    }

    [Fact]
    public void ShouldCutChecksAfterFirstFive_WhenModelSendsMore() {
        JsonArray checks = [.. Enumerable.Range(0, 7).Select(index => (JsonNode?)new JsonObject {
            ["text"] = index == 0 ? string.Concat(Enumerable.Repeat("word ", 40)) : index == 1 ? " " : $"Check {index}",
            ["state"] = "met",
        })];

        Classification? result = Classify(_ => { }, payload => payload["checks"] = checks);

        Assert.Equal(
            (4, true, "Check 2"),
            (result?.Checks.Count, result?.Checks[0].Text.EndsWith(ellipsis, StringComparison.Ordinal), result?.Checks[1].Text));
    }

    [Theory]
    [InlineData("Senior utvikler", "senior")]
    [InlineData("Tech lead", "lead")]
    [InlineData("Utvikler", "mid")]
    public void ShouldTakeLabelFromTitle_WhenTitleNamesLevel(string title, string label) {
        Classification? result = Classify(
            _ => { }, payload => payload["label"] = "mid", MatchingFixtures.Ad with { Title = title });

        Assert.Equal(label, result?.Label);
    }

    private static Classification? Classify(
        Action<JsonObject> editMatch, Action<JsonObject>? editPayload = null, AdView? ad = null) {
        JsonObject payload = MatchingFixtures.Payload();
        editMatch(payload["match"]!.AsObject());
        editPayload?.Invoke(payload);
        AdAssessment assessment = MatchingEntry.ParseAssessment(payload.ToJsonString())!;
        return MatchingEntry.ToClassification(
            assessment,
            ad ?? MatchingFixtures.Ad,
            MatchingFixtures.EvidenceIds,
            MatchingFixtures.EducationIds,
            MatchingFixtures.Narrow,
            MatchingFixtures.Version);
    }

    private static string Canonical(Classification result) => GoldenJson.Row(
        ("label", result.Label),
        ("summary", result.Summary),
        ("requires_master", result.RequiresMaster),
        ("is_programming_job", result.IsProgrammingJob),
        ("display_title", result.DisplayTitle),
        ("role_type", result.RoleType),
        ("technologies", result.Technologies),
        ("requirements", result.Requirements),
        ("assessment", MatchingEntry.EncodeMatch(result.Match, result.Checks)));
}
