using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Providers;

internal sealed class AnthropicClient(ApiSender sender, string key, string model, bool supportsEffort) : IModelClient {
    private const string messagesPath = "/v1/messages";
    private const string modelsPath = "/v1/models/";
    private const int maxOutputTokens = 4096;
    private const string effort = "low";
    private const string textType = "text";

    private static readonly string label = ProviderName.Anthropic.ToString();

    private static readonly Dictionary<string, StopKind> stopKinds = new(StringComparer.Ordinal) {
        ["refusal"] = StopKind.Refusal,
        ["max_tokens"] = StopKind.Truncated,
        ["model_context_window_exceeded"] = StopKind.Truncated,
    };

    public async Task<ModelReply> ClassifyAsync(
        ClassifyPrompt prompt, JsonObject schema, CancellationToken cancellationToken) {
        byte[] body = RequestBodies.Write(writer => WriteMessage(writer, prompt, schema));
        string url = ProviderHosts.AnthropicUrl + messagesPath;
        ApiReply reply = await sender
            .SendAsync(() => Authorized(RequestBodies.Post(url, body)), cancellationToken)
            .ConfigureAwait(false);
        if (!reply.IsSuccess) {
            throw ApiErrors.ToException(reply, anthropic: true, key);
        }
        return reply.Read(label, ReadMessage);
    }

    public async Task<KeyCheck> CheckKeyAsync(CancellationToken cancellationToken) {
        string url = ProviderHosts.AnthropicUrl + modelsPath + Uri.EscapeDataString(model);
        ApiReply reply = await sender
            .SendAsync(() => Authorized(RequestBodies.Get(url)), cancellationToken)
            .ConfigureAwait(false);
        if (!reply.IsSuccess) {
            return KeyChecks.Rejected(reply, anthropic: true, label, model, key);
        }
        reply.Read(label, root => root.GetProperty("id").GetString() ?? throw new FormatException("model id is null"));
        return new KeyCheck(true, KeyCheck.Works);
    }

    public void Dispose() => sender.Dispose();

    private static ModelReply ReadMessage(JsonElement root) {
        StringBuilder text = new();
        foreach (JsonElement block in root.GetProperty("content").EnumerateArray()) {
            if (block.GetProperty("type").GetString() == textType) {
                text.Append(ApiReply.Field(block, "text")?.GetString());
            }
        }
        TokenUsage usage = ReadUsage(root.GetProperty("usage"));
        string? reason = ApiReply.Field(root, "stop_reason")?.GetString();
        StopKind stop = reason is not null && stopKinds.TryGetValue(reason, out StopKind kind) ? kind : StopKind.Done;
        return new ModelReply(text.ToString(), usage, stop, null);
    }

    private static TokenUsage ReadUsage(JsonElement usage) {
        long input = usage.GetProperty("input_tokens").GetInt64();
        long output = usage.GetProperty("output_tokens").GetInt64();
        long cacheRead = ApiReply.Field(usage, "cache_read_input_tokens")?.GetInt64() ?? 0;
        if (ApiReply.Field(usage, "cache_creation") is JsonElement writes) {
            return new TokenUsage(
                input,
                writes.GetProperty("ephemeral_5m_input_tokens").GetInt64(),
                writes.GetProperty("ephemeral_1h_input_tokens").GetInt64(),
                cacheRead,
                output);
        }
        long written = ApiReply.Field(usage, "cache_creation_input_tokens")?.GetInt64() ?? 0;
        return new TokenUsage(input, written, 0, cacheRead, output);
    }

    private HttpRequestMessage Authorized(HttpRequestMessage request) {
        request.Headers.Add(ProviderHosts.AnthropicKeyHeader, key);
        request.Headers.Add(ProviderHosts.AnthropicVersionHeader, ProviderHosts.AnthropicVersion);
        return request;
    }

    private void WriteMessage(Utf8JsonWriter writer, ClassifyPrompt prompt, JsonObject schema) {
        writer.WriteNumber("max_tokens", maxOutputTokens);
        writer.WriteStartArray("messages");
        writer.WriteStartObject();
        writer.WriteString("role", "user");
        writer.WriteString("content", prompt.User);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteString("model", model);
        writer.WriteStartObject("output_config");
        writer.WriteStartObject("format");
        writer.WriteString("type", "json_schema");
        writer.WritePropertyName("schema");
        schema.WriteTo(writer);
        writer.WriteEndObject();
        if (supportsEffort) {
            writer.WriteString("effort", effort);
        }
        writer.WriteEndObject();
        writer.WriteStartArray("system");
        writer.WriteStartObject();
        writer.WriteString("type", textType);
        writer.WriteString("text", prompt.System);
        writer.WriteStartObject("cache_control");
        writer.WriteString("type", "ephemeral");
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndArray();
    }
}
