using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class ClassifyPromptsTests {
    private static readonly DateOnly assessmentDate = new(2026, 10, 9);

    [Theory]
    [InlineData("generic_utvikler")]
    [InlineData("two_years")]
    [InlineData("java_project")]
    public void ShouldEqualGolden_WhenBuildingAdJson(string caseId) {
        AdView ad = MatchingCases.Ad(MatchingCases.All()[caseId]);

        PromptText prompt = MatchingEntry.BuildPrompt(ad, "system", assessmentDate, []);

        Assert.Equal(MatchingFixtures.GoldenText("Prompts", $"user-{caseId}.txt"), prompt.User);
    }

    [Fact]
    public void ShouldWriteEmptyDescription_WhenAdHasNone() {
        AdView ad = MatchingCases.Ad(MatchingCases.All()["unknown"]) with { Description = null };

        PromptText prompt = MatchingEntry.BuildPrompt(ad, "system", assessmentDate, []);

        Assert.Equal(MatchingFixtures.GoldenText("Prompts", "user-no-description.txt"), prompt.User);
    }
}
