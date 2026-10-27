using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;
using static Jobranger.Cli.Tests.Mocks.ProviderFixtures;
using ProviderClients = Jobranger.Cli.Features.Providers.Providers;

namespace Jobranger.Cli.Tests.Features.Providers;

public sealed class ProvidersTests {
    private const string routerKey = "sk-fake-router-key-0001";

    private static readonly Dictionary<string, string?> savedKeys = new() {
        ["anthropic_api_key"] = AnthropicKey,
        ["openai_api_key"] = OpenAIKey,
        ["openrouter_api_key"] = routerKey,
    };

    [Theory]
    [InlineData(null, Sonnet)]
    [InlineData("Anthropic", null)]
    public async Task ShouldThrowSetupError_WhenNoModelIsChosen(string? providerName, string? model) {
        ProviderName? provider = providerName is null ? null : Enum.Parse<ProviderName>(providerName);
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());

        ModelSetupException error = await Assert.ThrowsAsync<ModelSetupException>(
            () => CreateAsync(handler, new ModelConfig { Provider = provider, Model = model }, savedKeys));

        Assert.Equal("no model chosen, run init to pick a provider and model", error.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task ShouldThrowSetupError_WhenNoKeyIsSaved(string? saved) {
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());
        ModelConfig config = new() { Provider = ProviderName.OpenRouter, Model = RouterModel };

        ModelSetupException error = await Assert.ThrowsAsync<ModelSetupException>(
            () => CreateAsync(handler, config, new Dictionary<string, string?> { ["openrouter_api_key"] = saved }));

        Assert.Equal("no API key saved for OpenRouter", error.Message);
    }

    [Fact]
    public async Task ShouldThrowSetupError_WhenKeyIsNotAscii() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());
        ModelConfig config = new() { Provider = ProviderName.OpenAI, Model = GptMini };

        ModelSetupException error = await Assert.ThrowsAsync<ModelSetupException>(
            () => CreateAsync(handler, config, new Dictionary<string, string?> { ["openai_api_key"] = "fake\u200Bkey-0001" }));

        Assert.Equal("OpenAI API key must be printable ASCII", error.Message);
    }

    [Fact]
    public async Task ShouldThrowSetupError_WhenOllamaUrlIsInvalid() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());
        ModelConfig config = new() { Provider = ProviderName.Ollama, Model = OllamaModel, OllamaUrl = "http://[::1" };

        ModelSetupException error = await Assert.ThrowsAsync<ModelSetupException>(
            () => CreateAsync(handler, config, savedKeys));

        Assert.Equal("ollama_url 'http://[::1' is not an http or https URL with a host", error.Message);
    }

    [Theory]
    [InlineData("Anthropic", Sonnet, "https://api.anthropic.com/v1/models/claude-sonnet-5-5", "x-api-key", AnthropicKey)]
    [InlineData("OpenAI", GptMini, "https://api.openai.com/v1/models/gpt-5-mini", "Authorization", "Bearer " + OpenAIKey)]
    [InlineData("OpenRouter", RouterModel, "https://openrouter.ai/api/v1/key", "Authorization", "Bearer " + routerKey)]
    public async Task ShouldUseOwnKeyAndOfficialUrl_WhenProviderIsBuilt(
        string providerName, string model, string url, string header, string value) {
        ProviderName provider = Enum.Parse<ProviderName>(providerName);
        using FakeApiHandler handler = FakeApiHandler.Json(401, ErrorBody);
        ModelConfig config = new() { Provider = provider, Model = model };
        using IModelClient client = await CreateAsync(handler, config, savedKeys);

        await client.CheckKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((url, value), (handler.Last.Uri.ToString(), handler.Last.Header(header)));
    }

    [Fact]
    public async Task ShouldCallOllamaUnderRoot_WhenUrlEndsWithApiPath() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, Completion("ollama_completion.json"));
        ModelConfig config = new() { Provider = ProviderName.Ollama, Model = OllamaModel, OllamaUrl = OllamaRoot + "/v1/" };
        using IModelClient client = await CreateAsync(handler, config, []);

        await client.ClassifyAsync(Prompt, OpenAISchema, TestContext.Current.CancellationToken);

        Assert.Equal((OllamaRoot + "/v1/chat/completions", "Bearer ollama"),
            (handler.Last.Uri.ToString(), handler.Last.Header("Authorization")));
    }

    [Fact]
    public async Task ShouldSendTrimmedKey_WhenSavedKeyHasWhitespace() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());
        ModelConfig config = new() { Provider = ProviderName.Anthropic, Model = Sonnet };
        using IModelClient client = await CreateAsync(
            handler, config, new Dictionary<string, string?> { ["anthropic_api_key"] = $"  {AnthropicKey}\n" });

        await client.ClassifyAsync(Prompt, AnthropicSchema, TestContext.Current.CancellationToken);

        Assert.Equal(AnthropicKey, handler.Last.Header("x-api-key"));
    }

    [Fact]
    public async Task ShouldRetryFailedCall_WhenClientIsBuilt() {
        using FakeApiHandler handler = FakeApiHandler.Json(500, ErrorBody);
        ModelConfig config = new() { Provider = ProviderName.Anthropic, Model = Sonnet };
        FakeTimeProvider time = ApiClock.Create();
        ProviderClients providers = new(() => handler, time);
        using IModelClient client = await providers.CreateAsync(config, Lookup(savedKeys), supportsEffort: true,
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ApiException>(
            () => ApiClock.RunAsync(time, client.CheckKeyAsync(TestContext.Current.CancellationToken)));

        Assert.Equal(5, handler.Requests.Count);
    }

    [Theory]
    [InlineData("Anthropic", Sonnet, AnthropicTimeout)]
    [InlineData("OpenAI", GptMini, "Request timed out.")]
    public async Task ShouldUseSdkTimeoutText_WhenProviderTimesOut(string providerName, string model, string message) {
        using FakeApiHandler handler = FakeApiHandler.Hanging();
        ModelConfig config = new() { Provider = Enum.Parse<ProviderName>(providerName), Model = model };
        FakeTimeProvider time = ApiClock.Create();
        ProviderClients providers = new(() => handler, time);
        using IModelClient client = await providers.CreateAsync(config, Lookup(savedKeys), supportsEffort: true,
            TestContext.Current.CancellationToken);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ApiClock.RunAsync(time, client.CheckKeyAsync(TestContext.Current.CancellationToken)));

        Assert.Equal(message, error.Message);
    }

    private static Task<IModelClient> CreateAsync(
        FakeApiHandler handler, ModelConfig config, Dictionary<string, string?> keys) {
        ProviderClients providers = new(() => handler, ApiClock.Create());
        return providers.CreateAsync(config, Lookup(keys), supportsEffort: true, TestContext.Current.CancellationToken);
    }

    private static Func<string, CancellationToken, Task<string?>> Lookup(Dictionary<string, string?> keys) =>
        (name, cancellationToken) => Task.FromResult(keys.GetValueOrDefault(name));
}
