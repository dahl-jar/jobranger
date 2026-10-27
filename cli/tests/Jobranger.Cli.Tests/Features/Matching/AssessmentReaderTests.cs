using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class AssessmentReaderTests {
    [Fact]
    public void ShouldParseFixtureReply_WhenEveryFieldIsValid() {
        AdAssessment? assessment = MatchingEntry.ParseAssessment(MatchingFixtures.Payload().ToJsonString());

        Assert.Equal(
            ("none", "backend", "met", "strong", 1),
            (assessment?.Label, assessment?.RoleType, assessment?.Checks[0].State, assessment?.Match.Status,
                assessment?.Match.Evidence.Count));
    }

    [Theory]
    [InlineData("""{"extra": 1}""")]
    [InlineData("""{"label": "error"}""")]
    [InlineData("""{"role_type": "sales"}""")]
    [InlineData("""{"summary": 5}""")]
    [InlineData("""{"technologies": [1]}""")]
    [InlineData("""{"technologies": "Java"}""")]
    [InlineData("""{"display_title": 5}""")]
    [InlineData("""{"is_programming_job": "maybe"}""")]
    [InlineData("""{"is_programming_job": 2}""")]
    [InlineData("""{"checks": [{"text": "Java", "state": "met", "extra": 1}]}""")]
    [InlineData("""{"checks": [{"text": "Java", "state": "done"}]}""")]
    [InlineData("""{"checks": [{"text": "Java"}]}""")]
    [InlineData("""{"match": {"status": "excellent"}}""")]
    [InlineData("""{"match": {"evidence": [1]}}""")]
    [InlineData("""{"match": {"required_experience_years": "three"}}""")]
    [InlineData("""{"match": {"extra": 1}}""")]
    public void ShouldRejectReply_WhenFieldBreaksSchema(string overrides) {
        string json = MatchingFixtures.Merge(MatchingFixtures.Payload(), overrides).ToJsonString();

        Assert.Null(MatchingEntry.ParseAssessment(json));
    }

    [Fact]
    public void ShouldRejectReply_WhenFieldIsMissing() {
        JsonObject payload = MatchingFixtures.Payload();
        payload.Remove("summary");

        Assert.Null(MatchingEntry.ParseAssessment(payload.ToJsonString()));
    }

    [Fact]
    public void ShouldRejectReply_WhenKeyIsMisspelled() {
        JsonObject payload = MatchingFixtures.Payload();
        payload["summmary"] = payload["summary"]!.DeepClone();
        payload.Remove("summary");

        Assert.Null(MatchingEntry.ParseAssessment(payload.ToJsonString()));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    public void ShouldRejectReply_WhenNotAnObject(string json) {
        Assert.Null(MatchingEntry.ParseAssessment(json));
    }

    [Theory]
    [InlineData("\\ud800")]
    [InlineData("\\udc00x")]
    public void ShouldRejectReply_WhenTextHasLoneSurrogate(string escape) {
        string json = MatchingFixtures.Payload().ToJsonString()
            .Replace("\"summary\":\"", "\"summary\":\"" + escape, StringComparison.Ordinal);

        Assert.Null(MatchingEntry.ParseAssessment(json));
    }

    [Theory]
    [InlineData("\"3\"", 3.0)]
    [InlineData("\"\u00a02.5\u2003\"", 2.5)]
    [InlineData("true", 1.0)]
    [InlineData("false", 0.0)]
    [InlineData("2", 2.0)]
    public void ShouldReadYears_WhenPydanticWouldCoerceThem(string years, double expected) {
        string json = MatchingFixtures.Payload().ToJsonString()
            .Replace("\"required_experience_years\":null", "\"required_experience_years\":" + years, StringComparison.Ordinal);

        Assert.Equal(expected, MatchingEntry.ParseAssessment(json)?.Match.RequiredExperienceYears);
    }

    [Theory]
    [InlineData("\"yes\"", true)]
    [InlineData("\"False\"", false)]
    [InlineData("1", true)]
    [InlineData("0.0", false)]
    public void ShouldReadFlag_WhenPydanticWouldCoerceIt(string flag, bool expected) {
        string json = MatchingFixtures.Payload().ToJsonString()
            .Replace("\"requires_master\":false", "\"requires_master\":" + flag, StringComparison.Ordinal);

        Assert.Equal(expected, MatchingEntry.ParseAssessment(json)?.RequiresMaster);
    }

    [Fact]
    public void ShouldTakeLastValue_WhenKeyRepeats() {
        string json = MatchingFixtures.Payload().ToJsonString()
            .Replace("\"label\":\"none\"", "\"label\":\"none\",\"label\":\"mid\"", StringComparison.Ordinal);

        Assert.Equal("mid", MatchingEntry.ParseAssessment(json)?.Label);
    }
}
