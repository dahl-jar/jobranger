using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Tests.Mocks;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Matching;

public sealed class MatchingTests {
    [Theory]
    [InlineData(nameof(ProviderName.OpenAI))]
    [InlineData(nameof(ProviderName.OpenRouter))]
    [InlineData(nameof(ProviderName.Ollama))]
    public void ShouldGiveBareSchema_WhenProviderIsOpenAICompatible(string providerName) {
        JsonObject schema = MatchingEntry.SchemaFor(Enum.Parse<ProviderName>(providerName));

        Assert.True(JsonNode.DeepEquals(ProviderFixtures.OpenAISchema, schema), schema.ToJsonString());
    }

    [Fact]
    public void ShouldGiveAnthropicSchema_WhenProviderIsAnthropic() {
        JsonObject schema = MatchingEntry.SchemaFor(ProviderName.Anthropic);

        Assert.True(JsonNode.DeepEquals(ProviderFixtures.AnthropicSchema, schema), schema.ToJsonString());
    }
}
