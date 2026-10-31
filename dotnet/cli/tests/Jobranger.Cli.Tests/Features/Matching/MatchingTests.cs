using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class MatchingTests {
    [Fact]
    public void ShouldGiveBareSchema_WhenProviderIsOpenAICompatible() {
        JsonObject schema = MatchingEntry.SchemaFor(ProviderName.OpenAI);

        Assert.Equal(ProviderFixtures.OpenAISchema.ToJsonString(), schema.ToJsonString());
    }

    [Fact]
    public void ShouldGiveAnthropicSchema_WhenProviderIsAnthropic() {
        JsonObject schema = MatchingEntry.SchemaFor(ProviderName.Anthropic);

        Assert.Equal(ProviderFixtures.AnthropicSchema.ToJsonString(), schema.ToJsonString());
    }
}
