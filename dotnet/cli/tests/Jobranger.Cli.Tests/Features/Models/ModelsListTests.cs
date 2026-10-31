using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Models;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;
using static Jobranger.Cli.Tests.Mocks.ProviderFixtures;
using ModelList = Jobranger.Cli.Features.Models.Models;

namespace Jobranger.Cli.Tests.Features.Models;

public sealed class ModelsListTests {
    private const string routerName = "Vendor: Model One";
    private const string ollamaTags = """{"models":[{"name":"llama3.2:latest","size":1}]}""";
    private const string unreadableReply = "<html>busy</html>";
    private const string anthropicPage =
        """{"data":[{"type":"model","id":"claude-future-9","display_name":"Claude Future 9","lifecycle":"active"},"""
        + """{"type":"model","id":"claude-sonnet-5-5","display_name":"Claude Sonnet 5.5"},"""
        + """{"type":"model","id":"claude-haiku-5-5","display_name":"Claude Haiku 5.5","lifecycle":"active"}],"""
        + """ "has_more":false,"last_id":"claude-haiku-5-5"}""";

    private readonly FakeTimeProvider time = ApiClock.Create();

    [Fact]
    public async Task ShouldSortUnknownPricesLast_WhenListingModels() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, anthropicPage);

        IReadOnlyList<ModelInfo> models = await ListAsync(handler, ProviderName.Anthropic, AnthropicKey);

        Assert.Equal([
            new ModelInfo("claude-haiku-5-5", "Claude Haiku 5.5", 0.1m, 0.5m, Batch: true),
            new ModelInfo("claude-sonnet-5-5", "Claude Sonnet 5.5", 2m, 10m, Batch: true),
            new ModelInfo("claude-future-9", "Claude Future 9", null, null, Batch: true),
        ], models);
    }

    [Fact]
    public async Task ShouldHideModel_WhenLifecycleIsNotActive() {
        using FakeApiHandler handler = FakeApiHandler.Json(200,
            """{"data":[{"id":"claude-old-1","display_name":"Old","lifecycle":"deprecated"},"""
            + """{"id":"claude-new-1","display_name":"New","lifecycle":"active"}],"has_more":false}""");

        IReadOnlyList<ModelInfo> models = await ListAsync(handler, ProviderName.Anthropic, AnthropicKey);

        Assert.Equal(["claude-new-1"], models.Select(model => model.Id));
    }

    [Fact]
    public async Task ShouldStopPaging_WhenHasMoreIsMissing() {
        using FakeApiHandler handler = FakeApiHandler.Json(200,
            """{"data":[{"id":"claude-a","display_name":"A"}],"last_id":"claude-a"}""");

        await ListAsync(handler, ProviderName.Anthropic, AnthropicKey);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ShouldSendAuthHeaders_WhenListingAnthropic() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, anthropicPage);

        await ListAsync(handler, ProviderName.Anthropic, $" {AnthropicKey}\n");

        Assert.Equal(("https://api.anthropic.com/v1/models", AnthropicKey, "2023-06-01"),
            (handler.Last.Uri.ToString(), handler.Last.Header("x-api-key"), handler.Last.Header("anthropic-version")));
    }

    [Fact]
    public async Task ShouldFollowNextPage_WhenAnthropicHasMore() {
        using FakeApiHandler handler = FakeApiHandler.Sequence(
            () => FakeApiHandler.Response(200,
                """{"data":[{"id":"claude-a","display_name":"A"}],"has_more":true,"last_id":"claude-a"}"""),
            () => FakeApiHandler.Response(200,
                """{"data":[{"id":"claude-b","display_name":"B"}],"has_more":false,"last_id":"claude-b"}"""));

        IReadOnlyList<ModelInfo> models = await ListAsync(handler, ProviderName.Anthropic, AnthropicKey);

        Assert.Equal(("claude-a,claude-b", "?after_id=claude-a"),
            (string.Join(",", models.Select(model => model.Id)), handler.Last.Uri.Query));
    }

    [Fact]
    public async Task ShouldKeepOnlyChatModels_WhenListingOpenAI() {
        string[] ids = ["gpt-4.1", "gpt-4o-audio-preview", "gpt-realtime", "gpt-4o-transcribe", "gpt-4o-mini-tts",
            "gpt-image-1", "gpt-4o-search-preview", "dall-e-3", "text-embedding-3-small", "gpt-5-mini"];
        JsonArray data = [.. ids.Select(id => (JsonNode)new JsonObject { ["id"] = id, ["object"] = "model" })];
        using FakeApiHandler handler = FakeApiHandler.Json(200, new JsonObject { ["object"] = "list", ["data"] = data }.ToJsonString());

        IReadOnlyList<ModelInfo> models = await ListAsync(handler, ProviderName.OpenAI, OpenAIKey);

        Assert.Equal(("gpt-5-mini,gpt-4.1", "Bearer " + OpenAIKey),
            (string.Join(",", models.Select(model => model.Id)), handler.Last.Header("Authorization")));
    }

    [Theory]
    [InlineData("Anthropic", "   ")]
    [InlineData("OpenAI", null)]
    public async Task ShouldThrowAuthError_WhenNoKeyIsSaved(string providerName, string? key) {
        ProviderName provider = Enum.Parse<ProviderName>(providerName);
        using FakeApiHandler handler = FakeApiHandler.Json(200, anthropicPage);

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ListAsync(handler, provider, key));

        Assert.Equal((ApiErrorKind.Auth, $"no API key saved for {providerName}", 0),
            (error.Kind, error.Message, handler.Requests.Count));
    }

    [Fact]
    public async Task ShouldRefuseKey_WhenItIsNotAscii() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, anthropicPage);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ListAsync(handler, ProviderName.OpenAI, "fake\u200Bkey-0001"));

        Assert.Equal((ApiErrorKind.Auth, "OpenAI API key must be printable ASCII", 0),
            (error.Kind, error.Message, handler.Requests.Count));
    }

    [Theory]
    [InlineData("Anthropic", AnthropicKey)]
    [InlineData("OpenAI", OpenAIKey)]
    public async Task ShouldShowOnlyStatus_WhenKeyIsRejectedWhileListing(string providerName, string key) {
        using FakeApiHandler handler = FakeApiHandler.Json(401, """{"error":{"message":"bad key sk-proj-****abcd"}}""");

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ListAsync(handler, Enum.Parse<ProviderName>(providerName), key));

        Assert.Equal((ApiErrorKind.Auth, "HTTP 401"), (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldThrowOverloaded_WhenAnthropicListingGets529() {
        using FakeApiHandler handler = FakeApiHandler.Json(529, ErrorBody);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ListAsync(handler, ProviderName.Anthropic, AnthropicKey));

        Assert.Equal(ApiErrorKind.Overloaded, error.Kind);
    }

    [Theory]
    [InlineData("Anthropic", AnthropicKey, AnthropicTimeout)]
    [InlineData("OpenAI", OpenAIKey, "Request timed out.")]
    public async Task ShouldUseSdkTimeoutText_WhenListingTimesOut(string providerName, string key, string message) {
        using FakeApiHandler handler = FakeApiHandler.Hanging();

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ListAsync(handler, Enum.Parse<ProviderName>(providerName), key));

        Assert.Equal(message, error.Message);
    }

    [Fact]
    public async Task ShouldLeaveKeyOutOfError_WhenListingDetailShowsKey() {
        using FakeApiHandler handler = FakeApiHandler.Json(400, """{"error":{"message":"bad """ + OpenAIKey + "\"}}");

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ListAsync(handler, ProviderName.OpenAI, OpenAIKey));

        Assert.Equal("HTTP 400", error.Message);
    }

    [Theory]
    [InlineData("Anthropic", AnthropicKey, """{"data":[{"id":5,"display_name":"Five"}],"has_more":false}""")]
    [InlineData("OpenAI", OpenAIKey, """{"object":"list","data":[{"id":5}]}""")]
    [InlineData("OpenAI", OpenAIKey, "not json")]
    public async Task ShouldThrowUnreadable_WhenListingReplyIsWrong(string providerName, string key, string body) {
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ListAsync(handler, Enum.Parse<ProviderName>(providerName), key));

        Assert.Equal((ApiErrorKind.Server, $"{providerName} sent a reply jobranger could not read"), (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldConvertPerTokenPrices_WhenListingOpenRouter() {
        IReadOnlyList<ModelInfo> models = await ListRouterAsync(Entry());

        Assert.Equal([new ModelInfo(RouterModel, routerName, 0.5m, 1.5m, Batch: false)], models);
    }

    [Fact]
    public async Task ShouldAskForStructuredOutputModels_WhenListingOpenRouter() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, Listing(Entry()));

        await ListAsync(handler, ProviderName.OpenRouter, null);

        Assert.Equal("https://openrouter.ai/api/v1/models?supported_parameters=structured_outputs", handler.Last.Uri.ToString());
    }

    [Theory]
    [InlineData("-1", "-1")]
    [InlineData("NaN", "0.000001")]
    [InlineData("0.000001", "inf")]
    [InlineData("-1", "0.000001")]
    [InlineData("1e25", "0.000001")]
    public async Task ShouldLeavePriceUnknown_WhenOpenRouterPriceIsUnusable(string prompt, string completion) {
        IReadOnlyList<ModelInfo> models = await ListRouterAsync(Entry(pricing: Pricing(prompt, completion)));

        Assert.Equal([new ModelInfo(RouterModel, routerName, null, null, Batch: false)], models);
    }

    [Fact]
    public async Task ShouldRoundPrices_WhenOpenRouterPriceHasManyDecimals() {
        IReadOnlyList<ModelInfo> models = await ListRouterAsync(
            Entry(pricing: Pricing("0.0000000123456789", "0.0000000123456789")));

        Assert.Equal((0.012346m, 0.012346m), (models[0].PriceIn, models[0].PriceOut));
    }

    [Fact]
    public async Task ShouldListZeroPrice_WhenOpenRouterModelIsFree() {
        IReadOnlyList<ModelInfo> models = await ListRouterAsync(Entry(pricing: Pricing("0", "0")));

        Assert.Equal((0m, 0m), (models[0].PriceIn, models[0].PriceOut));
    }

    [Fact]
    public async Task ShouldListPricesAsNumbers_WhenOpenRouterSendsNumbers() {
        JsonObject pricing = new() { ["prompt"] = 0.0000005, ["completion"] = 0.0000015 };

        IReadOnlyList<ModelInfo> models = await ListRouterAsync(Entry(pricing: pricing));

        Assert.Equal((0.5m, 1.5m), (models[0].PriceIn, models[0].PriceOut));
    }

    [Theory]
    [InlineData("""{"name":"No Id","pricing":{"prompt":"0","completion":"0"}}""")]
    [InlineData("""{"id":"","name":"Empty Id"}""")]
    [InlineData("""{"id":"vendor/odd","name":5}""")]
    [InlineData("\"vendor/text\"")]
    public async Task ShouldSkipOpenRouterEntry_WhenItIsInvalid(string invalid) {
        IReadOnlyList<ModelInfo> models = await ListRouterAsync(JsonNode.Parse(invalid)!, Entry());

        Assert.Equal([RouterModel], models.Select(model => model.Id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ShouldNameModelById_WhenOpenRouterNameIsMissing(string? name) {
        JsonObject entry = Entry(id: "vendor/unnamed");
        entry.Remove("name");
        if (name is not null) {
            entry["name"] = name;
        }

        IReadOnlyList<ModelInfo> models = await ListRouterAsync(entry);

        Assert.Equal("vendor/unnamed", models[0].Name);
    }

    [Fact]
    public async Task ShouldThrowServerError_WhenOpenRouterListingIsMalformed() {
        using FakeApiHandler handler = FakeApiHandler.Text(200, unreadableReply);

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ListAsync(handler, ProviderName.OpenRouter, null));

        Assert.Equal((ApiErrorKind.Server, "OpenRouter sent a model list jobranger could not read"), (error.Kind, error.Message));
    }

    [Theory]
    [InlineData(404, "NotFound", "OpenRouter at https://openrouter.ai/api/v1 answered HTTP 404")]
    [InlineData(529, "Server", "OpenRouter at https://openrouter.ai/api/v1 answered HTTP 529")]
    [InlineData(403, "Auth", "HTTP 403")]
    public async Task ShouldMapListingStatus_WhenOpenRouterFails(int status, string kindName, string message) {
        using FakeApiHandler handler = FakeApiHandler.Json(status, "{}");

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ListAsync(handler, ProviderName.OpenRouter, null));

        Assert.Equal((Enum.Parse<ApiErrorKind>(kindName), message), (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldCarryRetryAfter_WhenOpenRouterIsRateLimited() {
        using FakeApiHandler handler = FakeApiHandler.Json(429, "{}", ("retry-after", "7"));

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ListAsync(handler, ProviderName.OpenRouter, null));

        Assert.Equal((ApiErrorKind.RateLimit, TimeSpan.FromSeconds(7)), (error.Kind, error.RetryAfter));
    }

    [Fact]
    public async Task ShouldDropRetryAfter_WhenOpenRouterFailsOnServer() {
        using FakeApiHandler handler = FakeApiHandler.Json(503, "{}", ("retry-after", "7"));

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ListAsync(handler, ProviderName.OpenRouter, null));

        Assert.Null(error.RetryAfter);
    }

    [Fact]
    public async Task ShouldNameTarget_WhenOpenRouterIsUnreachable() {
        using FakeApiHandler handler = FakeApiHandler.Refusing();

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ListAsync(handler, ProviderName.OpenRouter, null));

        Assert.Equal((ApiErrorKind.Connection, "Could not reach OpenRouter at https://openrouter.ai/api/v1"),
            (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldListOllamaTagsAtZeroPrice_WhenUrlEndsWithApiPath() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, ollamaTags);

        IReadOnlyList<ModelInfo> models = await ListAsync(handler, ProviderName.Ollama, null, "http://box:11434/v1/");

        Assert.Equal((new ModelInfo("llama3.2:latest", "llama3.2:latest", 0m, 0m, Batch: false), "http://box:11434/api/tags"),
            (models.Single(), handler.Last.Uri.ToString()));
    }

    [Fact]
    public async Task ShouldThrowServerError_WhenOllamaReplyIsNotTags() {
        using FakeApiHandler handler = FakeApiHandler.Text(200, "<html>other service</html>");

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ListAsync(handler, ProviderName.Ollama, null));

        Assert.Equal((ApiErrorKind.Server, "http://box:11434 answered, but not with an Ollama model list. Check ollama_url"),
            (error.Kind, error.Message));
    }

    [Theory]
    [InlineData(503, "Server", "Ollama at http://box:11434 answered HTTP 503")]
    [InlineData(401, "Auth", "HTTP 401")]
    public async Task ShouldNameOllamaStatus_WhenListingFails(int status, string kindName, string message) {
        using FakeApiHandler handler = FakeApiHandler.Json(status, "{}");

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ListAsync(handler, ProviderName.Ollama, null, OllamaCredentialRoot));

        Assert.Equal((Enum.Parse<ApiErrorKind>(kindName), message), (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldNameOllamaServe_WhenOllamaIsUnreachable() {
        using FakeApiHandler handler = FakeApiHandler.Refusing();

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ListAsync(handler, ProviderName.Ollama, null, OllamaCredentialRoot));

        Assert.Equal((ApiErrorKind.Connection, "No Ollama answered at http://box:11434. Start it with: ollama serve"),
            (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldThrowConnectionError_WhenOllamaUrlIsInvalid() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, ollamaTags);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ListAsync(handler, ProviderName.Ollama, null, "http://user:Secret42@[::1"));

        Assert.Equal((ApiErrorKind.Connection, "ollama_url 'http://[::1' is not an http or https URL with a host", 0),
            (error.Kind, error.Message, handler.Requests.Count));
    }

    [Fact]
    public async Task ShouldSendBasicAuth_WhenOllamaUrlHasUserinfo() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, ollamaTags);

        await ListAsync(handler, ProviderName.Ollama, null, OllamaCredentialRoot);

        Assert.Equal(BasicHeader(OllamaUser, OllamaPassword), handler.Last.Header("Authorization"));
    }

    [Fact]
    public async Task ShouldRejectProvider_WhenItIsUnknown() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, ollamaTags);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ListAsync(handler, (ProviderName)99, null));
    }

    private static JsonObject Entry(string id = RouterModel, JsonObject? pricing = null) => new() {
        ["id"] = id,
        ["name"] = routerName,
        ["pricing"] = pricing ?? Pricing("0.0000005", "0.0000015"),
    };

    private static JsonObject Pricing(string prompt, string completion) => new() {
        ["prompt"] = prompt,
        ["completion"] = completion,
    };

    private static string Listing(params JsonNode[] entries) =>
        new JsonObject { ["data"] = new JsonArray([.. entries.Select(entry => entry.DeepClone())]) }.ToJsonString();

    private async Task<IReadOnlyList<ModelInfo>> ListRouterAsync(params JsonNode[] entries) {
        using FakeApiHandler handler = FakeApiHandler.Json(200, Listing(entries));
        return await ListAsync(handler, ProviderName.OpenRouter, null);
    }

    private Task<IReadOnlyList<ModelInfo>> ListAsync(
        FakeApiHandler handler, ProviderName provider, string? key, string url = OllamaRoot) {
        ModelList models = new(() => handler, time);
        return ApiClock.RunAsync(time, models.ListAsync(provider, key, url, TestContext.Current.CancellationToken));
    }
}
