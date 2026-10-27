using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class StoredMatchTests {
    [Fact]
    public void ShouldRestoreMatch_WhenDecodingEncodedMatch() {
        Match match = MatchingFixtures.StrongMatch with { Status = "stretch", Gaps = ["Qt experience not documented"] };

        Match? decoded = MatchingEntry.DecodeMatch(MatchingEntry.EncodeMatch(match, []));

        Assert.Equal(match, decoded);
    }

    [Fact]
    public void ShouldStoreMatchTextUnescaped_WhenTextIsNotAscii() {
        string? stored = MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch with { Reason = "Krever norsk på høyt nivå" }, []);

        Assert.Contains("\"reason\": \"Krever norsk på høyt nivå\"", stored, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldStoreNothing_WhenMatchIsMissing() {
        Assert.Null(MatchingEntry.EncodeMatch(null, []));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"status": "strong"}""")]
    public void ShouldExcludeStoredMatch_WhenNotMatchObject(string? stored) {
        Assert.Null(MatchingEntry.DecodeMatch(stored));
    }

    [Theory]
    [InlineData("""{"version": ""}""")]
    [InlineData("""{"version": 1}""")]
    [InlineData("""{"required_experience_years": "3"}""")]
    [InlineData("""{"evidence": "x"}""")]
    [InlineData("""{"is_low_code": "false"}""")]
    public void ShouldExcludeStoredMatch_WhenFieldIsUnusable(string overrides) {
        string stored = MatchingFixtures.Merge(Stored(), overrides).ToJsonString();

        Assert.Null(MatchingEntry.DecodeMatch(stored));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("reason")]
    [InlineData("gaps")]
    [InlineData("blockers")]
    [InlineData("required_experience_years")]
    public void ShouldExcludeStoredMatch_WhenFieldIsMissing(string field) {
        JsonObject stored = Stored();
        stored.Remove(field);

        Assert.Null(MatchingEntry.DecodeMatch(stored.ToJsonString()));
    }

    [Fact]
    public void ShouldExcludeStoredMatch_WhenTextHasLoneSurrogate() {
        string stored = MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch, [])!
            .Replace("\"reason\": \"", "\"reason\": \"\\ud800", StringComparison.Ordinal);

        Assert.Null(MatchingEntry.DecodeMatch(stored));
    }

    private static JsonObject Stored() =>
        JsonNode.Parse(MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch, [])!)!.AsObject();
}
