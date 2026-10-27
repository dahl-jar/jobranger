using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using static Jobranger.Cli.Tests.Mocks.ProviderFixtures;

namespace Jobranger.Cli.Tests.Features.Providers;

public sealed class OpenAICompatClientTests {
    private const string openAIReply = "openai_completion.json";
    private const string routerReply = "openrouter_completion.json";
    private const string ollamaReply = "ollama_completion.json";
    private const string badRequestCheck =
        "OpenAI refused the key check. Check the key and its workspace or project settings";
    private const string ollamaRefused =
        "Ollama at http://box:11434 refused the request (HTTP 401). Check its access settings";
    private const string ollamaNotRunning = "No Ollama answered at http://box:11434. Start it with: ollama serve";

    [Theory]
    [InlineData("OpenAI", GptMini, openAIReply)]
    [InlineData("OpenRouter", RouterModel, routerReply)]
    [InlineData("Ollama", OllamaModel, ollamaReply)]
    public async Task ShouldSendGoldenBody_WhenCallingOpenAICompatible(string providerName, string model, string fixture) {
        ProviderName provider = Enum.Parse<ProviderName>(providerName);
        using FakeApiHandler handler = FakeApiHandler.Json(200, Completion(fixture));

        await ClassifyAsync(handler, provider, model, supportsEffort: provider == ProviderName.OpenAI);

        Assert.Equal(GoldenBody(provider.ToString().ToLowerInvariant()), handler.Last.Body);
    }

    [Fact]
    public async Task ShouldOmitReasoningEffort_WhenModelLacksEffort() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, Completion(openAIReply));

        await ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: false);

        Assert.False(JsonNode.Parse(handler.Last.Text)!.AsObject().ContainsKey("reasoning_effort"));
    }

    [Theory]
    [InlineData("OpenAI", GptMini, "https://api.openai.com/v1/chat/completions", "Bearer " + OpenAIKey)]
    [InlineData("OpenRouter", RouterModel, "https://openrouter.ai/api/v1/chat/completions", "Bearer " + OpenAIKey)]
    [InlineData("Ollama", OllamaModel, "http://box:11434/v1/chat/completions", "Bearer " + OpenAIKey)]
    public async Task ShouldCallProviderUrl_WhenClassifying(string providerName, string model, string url, string auth) {
        ProviderName provider = Enum.Parse<ProviderName>(providerName);
        using FakeApiHandler handler = FakeApiHandler.Json(200, Completion(openAIReply));

        await ClassifyAsync(handler, provider, model, supportsEffort: false);

        Assert.Equal((url, auth, "application/json"),
            (handler.Last.Uri.ToString(), handler.Last.Header("Authorization"), handler.Last.Header("Accept")));
    }

    [Theory]
    [InlineData("OpenAI", GptMini, openAIReply)]
    [InlineData("OpenRouter", RouterModel, routerReply)]
    [InlineData("Ollama", OllamaModel, ollamaReply)]
    public async Task ShouldReturnReplyText_WhenModelAnswers(string providerName, string model, string fixture) {
        ProviderName provider = Enum.Parse<ProviderName>(providerName);
        using FakeApiHandler handler = FakeApiHandler.Json(200, Completion(fixture));

        ModelReply reply = await ClassifyAsync(handler, provider, model, supportsEffort: false);

        Assert.Equal((ReplyText, StopKind.Done), (reply.Text, reply.Stop));
    }

    [Fact]
    public async Task ShouldSubtractCachedTokens_WhenReplyCountsCache() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, Completion(openAIReply));

        ModelReply reply = await ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: true);

        Assert.Equal(new TokenUsage(500, 0, 0, 2500, 400), reply.Usage);
    }

    [Fact]
    public async Task ShouldCountAllPromptTokens_WhenCachedCountIsNull() {
        string body = Completion(openAIReply, reply => reply["usage"]!["prompt_tokens_details"]!["cached_tokens"] = null);
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: true);

        Assert.Equal(new TokenUsage(3000, 0, 0, 0, 400), reply.Usage);
    }

    [Fact]
    public async Task ShouldReturnNoUsage_WhenReplyHasNone() {
        string body = Completion(openAIReply, reply => reply.Remove("usage"));
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: true);

        Assert.Null(reply.Usage);
    }

    [Fact]
    public async Task ShouldUseReportedCost_WhenProviderIsOpenRouter() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, Completion(routerReply));

        ModelReply reply = await ClassifyAsync(handler, ProviderName.OpenRouter, RouterModel, supportsEffort: false);

        Assert.Equal(0.0042m, reply.ReportedCostUsd);
    }

    [Fact]
    public async Task ShouldIgnoreCost_WhenProviderIsNotOpenRouter() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, Completion(routerReply));

        ModelReply reply = await ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: false);

        Assert.Null(reply.ReportedCostUsd);
    }

    [Fact]
    public async Task ShouldReturnRefusalText_WhenModelRefuses() {
        string body = Completion(openAIReply, reply => {
            reply["choices"]![0]!["message"]!["content"] = null;
            reply["choices"]![0]!["message"]!["refusal"] = "I can't help.";
        });
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: true);

        Assert.Equal(("I can't help.", StopKind.Refusal), (reply.Text, reply.Stop));
    }

    [Theory]
    [InlineData("length", "Truncated")]
    [InlineData("content_filter", "Refusal")]
    [InlineData("stop", "Done")]
    public async Task ShouldMapFinishReason_WhenModelStops(string reason, string stopName) {
        StopKind stop = Enum.Parse<StopKind>(stopName);
        string body = Completion(openAIReply, reply => reply["choices"]![0]!["finish_reason"] = reason);
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: true);

        Assert.Equal(stop, reply.Stop);
    }

    [Fact]
    public async Task ShouldReturnEmptyText_WhenReplyHasNoContent() {
        string body = Completion(openAIReply, reply => reply["choices"]![0]!["message"]!["content"] = null);
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: true);

        Assert.Equal(string.Empty, reply.Text);
    }

    [Theory]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"x\"}}],\"usage\":{\"prompt_tokens\":\"many\",\"completion_tokens\":1}}")]
    [InlineData("hello")]
    public async Task ShouldThrowUnreadable_WhenReplyShapeIsWrong(string body) {
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: true));

        Assert.Equal((ApiErrorKind.Server, "OpenAI sent a reply jobranger could not read"), (error.Kind, error.Message));
    }

    [Theory]
    [InlineData("OpenAI", GptMini)]
    [InlineData("OpenRouter", RouterModel)]
    public async Task ShouldShowDetail_WhenHostIsOfficial(string providerName, string model) {
        using FakeApiHandler handler = FakeApiHandler.Json(400, ErrorBody);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ClassifyAsync(handler, Enum.Parse<ProviderName>(providerName), model, supportsEffort: false));

        Assert.Equal("HTTP 400: failed", error.Message);
    }

    [Fact]
    public async Task ShouldHideDetail_WhenHostIsOllama() {
        using FakeApiHandler handler = FakeApiHandler.Json(400, ErrorBody);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ClassifyAsync(handler, ProviderName.Ollama, OllamaModel, supportsEffort: false));

        Assert.Equal("HTTP 400", error.Message);
    }

    [Fact]
    public async Task ShouldThrowServerError_WhenOpenAIGets529() {
        using FakeApiHandler handler = FakeApiHandler.Json(529, ErrorBody);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: true));

        Assert.Equal((ApiErrorKind.Server, "HTTP 529: failed"), (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldCarryRetryAfter_WhenRateLimited() {
        using FakeApiHandler handler = FakeApiHandler.Json(429, ErrorBody, ("retry-after", "30"));

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => ClassifyAsync(handler, ProviderName.OpenAI, GptMini, supportsEffort: true));

        Assert.Equal((ApiErrorKind.RateLimit, TimeSpan.FromSeconds(30)), (error.Kind, error.RetryAfter));
    }

    [Theory]
    [InlineData("OpenAI", GptMini, 200, "{\"id\":\"gpt-5-mini\",\"object\":\"model\"}", "/v1/models/gpt-5-mini", true, "Key works")]
    [InlineData("OpenAI", GptMini, 401, ErrorBody, "/v1/models/gpt-5-mini", false, "OpenAI rejected the key")]
    [InlineData("OpenAI", GptMini, 404, ErrorBody, "/v1/models/gpt-5-mini", false, "OpenAI has no model named gpt-5-mini")]
    [InlineData("OpenAI", GptMini, 413, ErrorBody, "/v1/models/gpt-5-mini", false, badRequestCheck + ". OpenAI said: failed")]
    [InlineData("OpenRouter", RouterModel, 200, "{\"data\":{\"label\":\"fake-key\"}}", "/api/v1/key", true, "Key works")]
    [InlineData("OpenRouter", RouterModel, 401, ErrorBody, "/api/v1/key", false, "OpenRouter rejected the key")]
    public async Task ShouldCheckKeyAtProviderEndpoint_WhenKeyCheckRuns(
        string providerName, string model, int status, string body, string path, bool ok, string message) {
        ProviderName provider = Enum.Parse<ProviderName>(providerName);
        using FakeApiHandler handler = FakeApiHandler.Json(status, body);
        using OpenAICompatClient client = Client(handler, provider, model, supportsEffort: false);

        KeyCheck check = await client.CheckKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((new KeyCheck(ok, message), path), (check, handler.Last.Uri.AbsolutePath));
    }

    [Theory]
    [InlineData("OpenAI", GptMini, "{\"id\":5,\"object\":\"model\"}")]
    [InlineData("OpenRouter", RouterModel, "{\"data\":\"x\"}")]
    public async Task ShouldThrowUnreadable_WhenKeyCheckReplyIsWrong(string providerName, string model, string body) {
        ProviderName provider = Enum.Parse<ProviderName>(providerName);
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);
        using OpenAICompatClient client = Client(handler, provider, model, supportsEffort: false);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => client.CheckKeyAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ApiErrorKind.Server, error.Kind);
    }

    [Theory]
    [InlineData("llama3.2", true, "Ollama has llama3.2")]
    [InlineData("llama3.2:latest", true, "Ollama has llama3.2")]
    [InlineData("qwen3:8b", false, "Ollama has no model named llama3.2. Pull it with: ollama pull llama3.2")]
    public async Task ShouldFindModelInTags_WhenCheckingOllama(string installed, bool ok, string message) {
        using FakeApiHandler handler = FakeApiHandler.Json(200, "{\"models\":[{\"name\":\"" + installed + "\"}]}");
        using OpenAICompatClient client = Client(handler, ProviderName.Ollama, OllamaModel, supportsEffort: false);

        KeyCheck check = await client.CheckKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((new KeyCheck(ok, message), "http://box:11434/api/tags"), (check, handler.Last.Uri.ToString()));
    }

    [Theory]
    [InlineData(401, ollamaRefused)]
    [InlineData(429, "Ollama at http://box:11434 is limiting requests (HTTP 429). Try again later")]
    [InlineData(500, "Ollama at http://box:11434 failed (HTTP 500). Check the Ollama logs")]
    [InlineData(529, "Ollama at http://box:11434 failed (HTTP 529). Check the Ollama logs")]
    [InlineData(404, "Ollama at http://box:11434 answered HTTP 404. Check ollama_url")]
    public async Task ShouldNameOllamaStatus_WhenTagsRequestFails(int status, string message) {
        using FakeApiHandler handler = FakeApiHandler.Json(status, ErrorBody);
        using OpenAICompatClient client = Client(handler, ProviderName.Ollama, OllamaModel, supportsEffort: false);

        KeyCheck check = await client.CheckKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new KeyCheck(false, message), check);
    }

    [Theory]
    [InlineData("{\"models\":\"x\"}")]
    [InlineData("<html>other service</html>")]
    public async Task ShouldNameOllamaServe_WhenAnotherServiceAnswers(string body) {
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);
        using OpenAICompatClient client = Client(handler, ProviderName.Ollama, OllamaModel, supportsEffort: false);

        KeyCheck check = await client.CheckKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new KeyCheck(false, ollamaNotRunning), check);
    }

    [Fact]
    public async Task ShouldNameOllamaServe_WhenOllamaIsUnreachable() {
        using FakeApiHandler handler = FakeApiHandler.Refusing();
        using OpenAICompatClient client = Client(handler, ProviderName.Ollama, OllamaModel, supportsEffort: false);

        KeyCheck check = await client.CheckKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new KeyCheck(false, ollamaNotRunning), check);
    }

    [Fact]
    public async Task ShouldHideOllamaPassword_WhenKeyCheckFails() {
        using FakeApiHandler handler = FakeApiHandler.Json(401, ErrorBody);
        using OpenAICompatClient client = new(
            OpenAISender(handler), ProviderName.Ollama, OpenAIKey, OllamaModel,
            OllamaCredentialRoot, supportsEffort: false);

        KeyCheck check = await client.CheckKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ollamaRefused, check.Message);
    }

    [Fact]
    public async Task ShouldSendBasicAuth_WhenOllamaUrlHasUserinfo() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, Completion(ollamaReply));
        using OpenAICompatClient client = new(
            OpenAISender(handler), ProviderName.Ollama, OpenAICompatClient.OllamaKey, OllamaModel,
            OllamaCredentialRoot, supportsEffort: false);

        await client.ClassifyAsync(Prompt, OpenAISchema, TestContext.Current.CancellationToken);

        Assert.Equal("Basic dXNlcjpTZWNyZXQ0Mg==", handler.Last.Header("Authorization"));
    }

    private static OpenAICompatClient Client(
        FakeApiHandler handler, ProviderName provider, string model, bool supportsEffort) =>
        new(OpenAISender(handler), provider, OpenAIKey, model, OllamaRoot, supportsEffort);

    private static async Task<ModelReply> ClassifyAsync(
        FakeApiHandler handler, ProviderName provider, string model, bool supportsEffort) {
        using OpenAICompatClient client = Client(handler, provider, model, supportsEffort);
        return await client.ClassifyAsync(Prompt, OpenAISchema, TestContext.Current.CancellationToken);
    }
}
