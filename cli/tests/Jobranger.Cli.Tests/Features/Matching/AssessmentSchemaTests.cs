using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class AssessmentSchemaTests {
    [Fact]
    public void ShouldEqualGolden_WhenReadingAnthropicSchema() {
        Assert.Equal(MatchingFixtures.GoldenText("Schemas", "anthropic.json"), AssessmentSchema.Anthropic);
    }

    [Fact]
    public void ShouldEqualGolden_WhenReadingOpenAiSchema() {
        Assert.Equal(MatchingFixtures.GoldenText("Schemas", "openai.json"), AssessmentSchema.OpenAI);
    }
}
