using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Providers;

internal sealed class OpenAICompatClient(
    ApiSender sender, ProviderName provider, string key, string model, string ollamaRoot, bool supportsEffort)
    : IModelClient {
    public const string OllamaKey = "ollama";

    private const string completionsPath = "/chat/completions";
    private const string modelsPath = "/models/";
    private const string openRouterKeyPath = "/key";
    private const string ollamaDefaultTag = ":latest";
    private const string cacheKeyPrefix = "jobranger-";
    private const int cacheKeyHexChars = 16;
    private const int maxOutputTokens = 4096;
    private const string effort = "low";

    private static readonly Dictionary<string, StopKind> stopKinds = new(StringComparer.Ordinal) {
        ["length"] = StopKind.Truncated,
        ["content_filter"] = StopKind.Refusal,
    };

    private readonly string label = provider.ToString();
    private readonly string baseUrl = BaseUrlFor(provider, ollamaRoot);
    private readonly AuthenticationHeaderValue? basicAuth = OllamaUrls.BasicAuth(ollamaRoot);

    private string? OfficialKey => provider == ProviderName.Ollama ? null : key;

    public async Task<ModelReply> ClassifyAsync(
        ClassifyPrompt prompt, JsonObject schema, CancellationToken cancellationToken) {
        byte[] body = RequestBodies.Write(writer => WriteCompletion(writer, prompt, schema));
        ApiReply reply = await sender
            .SendAsync(() => Authorized(RequestBodies.Post(baseUrl + completionsPath, body)), cancellationToken)
            .ConfigureAwait(false);
        if (!reply.IsSuccess) {
            throw ApiErrors.ToException(reply, anthropic: false, OfficialKey);
        }
        return reply.Read(label, ReadCompletion);
    }

    public Task<KeyCheck> CheckKeyAsync(CancellationToken cancellationToken) => provider switch {
        ProviderName.Ollama => CheckOllamaAsync(cancellationToken),
        ProviderName.OpenRouter => CheckAsync(baseUrl + openRouterKeyPath, ReadRouterKey, cancellationToken),
        _ => CheckAsync(baseUrl + modelsPath + Uri.EscapeDataString(model), ReadModelId, cancellationToken),
    };

    public void Dispose() => sender.Dispose();

    private static bool ReadModelId(JsonElement root) =>
        root.GetProperty("id").GetString() is not null ? true : throw new FormatException("model id is null");

    private static bool ReadRouterKey(JsonElement root) =>
        root.GetProperty("data").ValueKind == JsonValueKind.Object
            ? true
            : throw new FormatException("key data is not an object");

    private static string CacheKey(string system) =>
        cacheKeyPrefix + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(system)))[..cacheKeyHexChars];

    private async Task<KeyCheck> CheckAsync(
        string url, Func<JsonElement, bool> read, CancellationToken cancellationToken) {
        ApiReply reply = await sender
            .SendAsync(() => Authorized(RequestBodies.Get(url)), cancellationToken)
            .ConfigureAwait(false);
        if (!reply.IsSuccess) {
            return KeyChecks.Rejected(reply, anthropic: false, label, model, key);
        }
        reply.Read(label, read);
        return new KeyCheck(true, KeyCheck.Works);
    }

    private static string BaseUrlFor(ProviderName provider, string ollamaRoot) => provider switch {
        ProviderName.OpenAI => ProviderHosts.OpenAIUrl,
        ProviderName.OpenRouter => ProviderHosts.OpenRouterUrl,
        ProviderName.Ollama => ollamaRoot + OllamaUrls.ApiPath,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "not an OpenAI-compatible provider"),
    };

    private async Task<KeyCheck> CheckOllamaAsync(CancellationToken cancellationToken) {
        string shown = OllamaUrls.WithoutUserinfo(ollamaRoot);
        KeyCheck notRunning = new(false, OllamaUrls.NotRunning(shown));
        ApiReply reply;
        try {
            reply = await sender
                .SendAsync(() => Authorized(RequestBodies.Get(ollamaRoot + OllamaUrls.TagsPath)), cancellationToken)
                .ConfigureAwait(false);
        } catch (ApiException) {
            return notRunning;
        }
        if (!reply.IsSuccess) {
            return new KeyCheck(false, OllamaStatus(shown, reply.Status));
        }
        string[] names;
        try {
            names = reply.Read(label, ProviderHosts.ReadOllamaTags);
        } catch (ApiException) {
            return notRunning;
        }
        return names.Contains(model) || names.Contains(model + ollamaDefaultTag)
            ? new KeyCheck(true, $"Ollama has {model}")
            : new KeyCheck(false, $"Ollama has no model named {model}. Pull it with: ollama pull {model}");
    }

    private static string OllamaStatus(string root, int status) => ApiErrors.KindOf(status, anthropic: false) switch {
        ApiErrorKind.Auth => $"Ollama at {root} refused the request (HTTP {status}). Check its access settings",
        ApiErrorKind.RateLimit => $"Ollama at {root} is limiting requests (HTTP {status}). Try again later",
        ApiErrorKind.Server => $"Ollama at {root} failed (HTTP {status}). Check the Ollama logs",
        _ => $"Ollama at {root} answered HTTP {status}. Check ollama_url",
    };

    private ModelReply ReadCompletion(JsonElement root) {
        JsonElement choice = root.GetProperty("choices").EnumerateArray().First();
        JsonElement message = choice.GetProperty("message");
        string? content = ApiReply.Field(message, "content")?.GetString();
        string? refusal = ApiReply.Field(message, "refusal")?.GetString();
        string? reason = ApiReply.Field(choice, "finish_reason")?.GetString();
        StopKind stop = refusal is not null ? StopKind.Refusal
            : reason is not null && stopKinds.TryGetValue(reason, out StopKind kind) ? kind
            : StopKind.Done;
        JsonElement? usage = ApiReply.Field(root, "usage");
        return new ModelReply(
            content ?? refusal ?? string.Empty,
            usage is JsonElement counts ? ReadUsage(counts) : null,
            stop,
            usage is JsonElement reported ? ReportedCost(reported) : null);
    }

    private static TokenUsage ReadUsage(JsonElement usage) {
        long prompt = usage.GetProperty("prompt_tokens").GetInt64();
        long output = usage.GetProperty("completion_tokens").GetInt64();
        long cached = ApiReply.Field(usage, "prompt_tokens_details") is JsonElement details
            ? ApiReply.Field(details, "cached_tokens")?.GetInt64() ?? 0
            : 0;
        return new TokenUsage(prompt - cached, 0, 0, cached, output);
    }

    private decimal? ReportedCost(JsonElement usage) =>
        provider == ProviderName.OpenRouter
        && ApiReply.Field(usage, "cost") is JsonElement cost
        && cost.ValueKind == JsonValueKind.Number
        && cost.TryGetDecimal(out decimal value)
            ? value
            : null;

    private HttpRequestMessage Authorized(HttpRequestMessage request) {
        request.Headers.Authorization = basicAuth ?? new AuthenticationHeaderValue(ProviderHosts.Bearer, key);
        return request;
    }

    private void WriteCompletion(Utf8JsonWriter writer, ClassifyPrompt prompt, JsonObject schema) {
        writer.WriteString("model", model);
        writer.WriteStartArray("messages");
        WriteMessage(writer, "system", prompt.System);
        WriteMessage(writer, "user", prompt.User);
        writer.WriteEndArray();
        writer.WriteNumber("max_completion_tokens", maxOutputTokens);
        if (provider == ProviderName.OpenAI) {
            writer.WriteString("prompt_cache_key", CacheKey(prompt.System));
            if (supportsEffort) {
                writer.WriteString("reasoning_effort", effort);
            }
        }
        writer.WriteStartObject("response_format");
        writer.WriteString("type", "json_schema");
        writer.WriteStartObject("json_schema");
        writer.WritePropertyName("schema");
        schema.WriteTo(writer);
        writer.WriteString("name", (string?)schema["title"] ?? throw new ArgumentException("schema has no title", nameof(schema)));
        writer.WriteBoolean("strict", true);
        writer.WriteEndObject();
        writer.WriteEndObject();
        if (provider == ProviderName.Ollama) {
            writer.WriteNumber("temperature", 0);
        }
        if (provider == ProviderName.OpenRouter) {
            writer.WriteStartObject("provider");
            writer.WriteBoolean("require_parameters", true);
            writer.WriteString("data_collection", "deny");
            writer.WriteEndObject();
        }
    }

    private static void WriteMessage(Utf8JsonWriter writer, string role, string content) {
        writer.WriteStartObject();
        writer.WriteString("role", role);
        writer.WriteString("content", content);
        writer.WriteEndObject();
    }
}
