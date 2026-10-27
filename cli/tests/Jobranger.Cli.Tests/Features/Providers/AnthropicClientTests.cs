using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;
using Jobranger.Cli.Tests.Mocks;
using static Jobranger.Cli.Tests.Mocks.ProviderFixtures;

namespace Jobranger.Cli.Tests.Features.Providers;

public sealed class AnthropicClientTests {
    private const string badRequestCheck =
        "Anthropic refused the key check. Check the key and its workspace or project settings";
    private const string unscopedMessage =
        "This API key is not scoped to a workspace, so this request must include the anthropic-workspace-id header";

    [Fact]
    public async Task ShouldSendGoldenBody_WhenCallingAnthropic() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());

        await ClassifyAsync(handler);

        Assert.Equal(GoldenBody("anthropic"), handler.Last.Body);
    }

    [Fact]
    public async Task ShouldOmitEffort_WhenModelLacksEffort() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());

        await ClassifyAsync(handler, supportsEffort: false);

        Assert.False(JsonNode.Parse(handler.Last.Text)!["output_config"]!.AsObject().ContainsKey("effort"));
    }

    [Fact]
    public async Task ShouldIgnoreEnv_WhenAnthropicBaseUrlIsSet() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());
        Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", "http://elsewhere.example.test");
        try {
            await ClassifyAsync(handler);
        } finally {
            Environment.SetEnvironmentVariable("ANTHROPIC_BASE_URL", null);
        }

        Assert.Equal("https://api.anthropic.com/v1/messages", handler.Last.Uri.ToString());
    }

    [Fact]
    public async Task ShouldSendAuthHeaders_WhenCalling() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());

        await ClassifyAsync(handler);

        Assert.Equal((AnthropicKey, "2023-06-01", "application/json"),
            (handler.Last.Header("x-api-key"), handler.Last.Header("anthropic-version"), handler.Last.Header("Accept")));
    }

    [Fact]
    public async Task ShouldReturnReplyText_WhenModelAnswers() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage());

        ModelReply reply = await ClassifyAsync(handler);

        Assert.Equal((ReplyText, StopKind.Done, (decimal?)null), (reply.Text, reply.Stop, reply.ReportedCostUsd));
    }

    [Fact]
    public async Task ShouldJoinTextBlocks_WhenReplyHasSeveral() {
        string body = AnthropicMessage(reply => reply["content"] = new JsonArray(
            new JsonObject { ["type"] = "text", ["text"] = "{\"a\":" },
            new JsonObject { ["type"] = "thinking", ["text"] = "skip" },
            new JsonObject { ["type"] = "text", ["text"] = "1}" }));
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler);

        Assert.Equal("{\"a\":1}", reply.Text);
    }

    [Fact]
    public async Task ShouldSkipOtherBlock_WhenItsTextIsNotString() {
        string body = AnthropicMessage(reply => reply["content"]!.AsArray().Add(
            new JsonObject { ["type"] = "tool_use", ["text"] = 5 }));
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler);

        Assert.Equal(ReplyText, reply.Text);
    }

    [Theory]
    [InlineData("refusal", "Refusal")]
    [InlineData("max_tokens", "Truncated")]
    [InlineData("model_context_window_exceeded", "Truncated")]
    [InlineData("end_turn", "Done")]
    public async Task ShouldMapStopReason_WhenModelStops(string reason, string stopName) {
        StopKind stop = Enum.Parse<StopKind>(stopName);
        using FakeApiHandler handler = FakeApiHandler.Json(200, AnthropicMessage(reply => reply["stop_reason"] = reason));

        ModelReply reply = await ClassifyAsync(handler);

        Assert.Equal(stop, reply.Stop);
    }

    [Fact]
    public async Task ShouldSplitCacheWrites_WhenReplyHasBreakdown() {
        string body = AnthropicMessage(reply => {
            reply["usage"]!["cache_creation_input_tokens"] = 300;
            reply["usage"]!["cache_creation"] = new JsonObject {
                ["ephemeral_5m_input_tokens"] = 200,
                ["ephemeral_1h_input_tokens"] = 100,
            };
        });
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler);

        Assert.Equal(new TokenUsage(500, 200, 100, 2500, 400), reply.Usage);
    }

    [Fact]
    public async Task ShouldCountCacheWritesAsFiveMinutes_WhenBreakdownIsMissing() {
        string body = AnthropicMessage(reply => {
            reply["usage"]!["cache_creation_input_tokens"] = 300;
            reply["usage"]!["cache_creation"] = null;
        });
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler);

        Assert.Equal(new TokenUsage(500, 300, 0, 2500, 400), reply.Usage);
    }

    [Fact]
    public async Task ShouldCountZeroCacheTokens_WhenCacheFieldsAreNull() {
        string body = AnthropicMessage(reply => {
            reply["usage"]!["cache_creation_input_tokens"] = null;
            reply["usage"]!["cache_creation"] = null;
            reply["usage"]!["cache_read_input_tokens"] = null;
        });
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ModelReply reply = await ClassifyAsync(handler);

        Assert.Equal(new TokenUsage(500, 0, 0, 0, 400), reply.Usage);
    }

    [Theory]
    [InlineData("{\"unexpected\":true}")]
    [InlineData("not json")]
    [InlineData("{\"content\":[{\"type\":\"text\",\"text\":\"x\"}],\"usage\":{\"input_tokens\":\"many\",\"output_tokens\":1}}")]
    public async Task ShouldThrowUnreadable_WhenReplyShapeIsWrong(string body) {
        using FakeApiHandler handler = FakeApiHandler.Json(200, body);

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ClassifyAsync(handler));

        Assert.Equal((ApiErrorKind.Server, "Anthropic sent a reply jobranger could not read"), (error.Kind, error.Message));
    }

    [Theory]
    [InlineData(403, "Auth", "HTTP 403")]
    [InlineData(413, "BadRequest", "HTTP 413: failed")]
    [InlineData(529, "Overloaded", "HTTP 529: failed")]
    public async Task ShouldThrowMappedError_WhenStatusFails(int status, string kindName, string message) {
        ApiErrorKind kind = Enum.Parse<ApiErrorKind>(kindName);
        using FakeApiHandler handler = FakeApiHandler.Json(status, ErrorBody);

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ClassifyAsync(handler));

        Assert.Equal((kind, message), (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldLeaveKeyOutOfError_WhenDetailShowsKey() {
        using FakeApiHandler handler = FakeApiHandler.Json(400, "{\"error\":{\"message\":\"bad " + AnthropicKey + "\"}}");

        ApiException error = await Assert.ThrowsAsync<ApiException>(() => ClassifyAsync(handler));

        Assert.Equal("HTTP 400", error.Message);
    }

    [Theory]
    [InlineData(200, "{\"id\":\"claude-sonnet-5-5\",\"type\":\"model\"}", true, "Key works")]
    [InlineData(401, ErrorBody, false, "Anthropic rejected the key")]
    [InlineData(404, ErrorBody, false, "Anthropic has no model named claude-sonnet-5-5")]
    [InlineData(400, "{\"error\":{\"message\":\"" + unscopedMessage + "\"}}", false,
        badRequestCheck + ". Anthropic said: " + unscopedMessage)]
    [InlineData(400, "{\"error\":{\"type\":\"invalid_request_error\"}}", false, badRequestCheck)]
    public async Task ShouldCheckKeyAgainstModel_WhenKeyCheckRuns(int status, string body, bool ok, string message) {
        using FakeApiHandler handler = FakeApiHandler.Json(status, body);
        using AnthropicClient client = new(AnthropicSender(handler), AnthropicKey, Sonnet, supportsEffort: true);

        KeyCheck check = await client.CheckKeyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((new KeyCheck(ok, message), "/v1/models/claude-sonnet-5-5"), (check, handler.Last.Uri.AbsolutePath));
    }

    [Theory]
    [InlineData(500, "Server", "HTTP 500")]
    [InlineData(529, "Overloaded", "HTTP 529")]
    public async Task ShouldThrowMappedError_WhenKeyCheckFailsOnServer(int status, string kindName, string message) {
        using FakeApiHandler handler = FakeApiHandler.Json(status, "{\"error\":{\"message\":\"down for " + AnthropicKey + "\"}}");
        using AnthropicClient client = new(AnthropicSender(handler), AnthropicKey, Sonnet, supportsEffort: true);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => client.CheckKeyAsync(TestContext.Current.CancellationToken));

        Assert.Equal((Enum.Parse<ApiErrorKind>(kindName), message), (error.Kind, error.Message));
    }

    [Fact]
    public async Task ShouldThrowUnreadable_WhenKeyCheckReplyHasNoId() {
        using FakeApiHandler handler = FakeApiHandler.Json(200, "{\"id\":5,\"type\":\"model\"}");
        using AnthropicClient client = new(AnthropicSender(handler), AnthropicKey, Sonnet, supportsEffort: true);

        ApiException error = await Assert.ThrowsAsync<ApiException>(
            () => client.CheckKeyAsync(TestContext.Current.CancellationToken));

        Assert.Equal(ApiErrorKind.Server, error.Kind);
    }

    private static async Task<ModelReply> ClassifyAsync(FakeApiHandler handler, bool supportsEffort = true) {
        using AnthropicClient client = new(AnthropicSender(handler), AnthropicKey, Sonnet, supportsEffort);
        return await client.ClassifyAsync(Prompt, AnthropicSchema, TestContext.Current.CancellationToken);
    }
}
