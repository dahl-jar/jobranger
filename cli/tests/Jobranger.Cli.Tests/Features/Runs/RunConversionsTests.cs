using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Runs;

public sealed class RunConversionsTests {
    [Fact]
    public void ShouldKeepMatchChecks_WhenStoringClassification() {
        Check[] checks = [new("Java services", "met")];
        Classification classification = new(
            "junior", null, false, true, null, "backend", [], [], MatchingFixtures.StrongMatch, checks);

        StoredClassification stored = RunConversions.ToStored(classification);

        Assert.Equal(MatchingEntry.EncodeMatch(MatchingFixtures.StrongMatch, checks), stored.Assessment);
    }

    [Fact]
    public void ShouldCountZeroTokens_WhenReplyHasNoUsage() {
        TokenCounts tokens = RunConversions.ToTokens(null);

        Assert.Equal(new TokenCounts(0, 0, 0, 0, 0), tokens);
    }
}
