using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Jobranger.Cli.Config;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Models;

internal static class ModelLists {
    private const string anthropicModelsUrl = ProviderHosts.AnthropicUrl + "/v1/models";
    private const string afterQuery = "?after_id=";
    private const string activeLifecycle = "active";
    private const string openAIModelsUrl = ProviderHosts.OpenAIUrl + "/models";
    private const string openAIModelPrefix = "gpt-";
    private const string openRouterModelsQuery = "/models?supported_parameters=structured_outputs";
    private const int maxPages = 100;
    private const decimal tokensPerMtok = 1_000_000m;
    private const int priceDecimals = 6;

    private static readonly decimal largestPerToken = decimal.MaxValue / tokensPerMtok;

    private static readonly string[] excludedParts = ["audio", "realtime", "transcribe", "tts", "image", "search"];

    public static async Task<List<ModelInfo>> AnthropicAsync(
        ApiSender sender, string key, CancellationToken cancellationToken) {
        string label = ProviderName.Anthropic.ToString();
        List<ModelInfo> models = [];
        string? after = null;
        for (int page = 0; page < maxPages; page++) {
            string url = after is null ? anthropicModelsUrl : anthropicModelsUrl + afterQuery + Uri.EscapeDataString(after);
            ApiReply reply = await sender.SendAsync(() => {
                HttpRequestMessage request = new(HttpMethod.Get, url);
                request.Headers.Add(ProviderHosts.AnthropicKeyHeader, key);
                request.Headers.Add(ProviderHosts.AnthropicVersionHeader, ProviderHosts.AnthropicVersion);
                return request;
            }, cancellationToken).ConfigureAwait(false);
            if (!reply.IsSuccess) {
                throw ApiErrors.ToException(reply, anthropic: true, key);
            }
            (List<ModelInfo> listed, string? next) = reply.Read(label, ReadAnthropicPage);
            models.AddRange(listed);
            if (next is null) {
                break;
            }
            after = next;
        }
        return models;
    }

    public static async Task<List<ModelInfo>> OpenAIAsync(
        ApiSender sender, string key, CancellationToken cancellationToken) {
        ApiReply reply = await sender.SendAsync(() => {
            HttpRequestMessage request = new(HttpMethod.Get, openAIModelsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue(ProviderHosts.Bearer, key);
            return request;
        }, cancellationToken).ConfigureAwait(false);
        if (!reply.IsSuccess) {
            throw ApiErrors.ToException(reply, anthropic: false, key);
        }
        return reply.Read(ProviderName.OpenAI.ToString(), root => root.GetProperty("data").EnumerateArray()
            .Select(model => model.GetProperty("id").GetString() ?? throw new FormatException("model id is null"))
            .Where(IsChatModel)
            .Select(id => Priced(ProviderName.OpenAI, id, id))
            .ToList());
    }

    public static async Task<List<ModelInfo>> OpenRouterAsync(ApiSender sender, CancellationToken cancellationToken) {
        string label = ProviderName.OpenRouter.ToString();
        string unreachable = $"Could not reach {label} at {ProviderHosts.OpenRouterUrl}";
        ApiReply reply = await GetOrUnreachableAsync(
            sender, ProviderHosts.OpenRouterUrl + openRouterModelsQuery, null, unreachable, cancellationToken)
            .ConfigureAwait(false);
        if (!reply.IsSuccess) {
            throw HostError(reply, label, ProviderHosts.OpenRouterUrl);
        }
        try {
            using JsonDocument document = JsonDocument.Parse(reply.Body);
            return [.. document.RootElement.GetProperty("data").EnumerateArray()
                .Select(ReadRouterModel)
                .OfType<ModelInfo>()];
        } catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) {
            throw new ApiException(ApiErrorKind.Server, $"{label} sent a model list jobranger could not read");
        }
    }

    public static async Task<List<ModelInfo>> OllamaAsync(
        ApiSender sender, string root, CancellationToken cancellationToken) {
        string shown = OllamaUrls.WithoutUserinfo(root);
        ApiReply reply = await GetOrUnreachableAsync(
            sender, root + OllamaUrls.TagsPath, OllamaUrls.BasicAuth(root), OllamaUrls.NotRunning(shown), cancellationToken)
            .ConfigureAwait(false);
        if (!reply.IsSuccess) {
            throw HostError(reply, ProviderName.Ollama.ToString(), shown);
        }
        try {
            using JsonDocument document = JsonDocument.Parse(reply.Body);
            return [.. ProviderHosts.ReadOllamaTags(document.RootElement)
                .Select(name => new ModelInfo(name, name, 0m, 0m, Batch: false))];
        } catch (Exception error) when (error is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException) {
            throw new ApiException(
                ApiErrorKind.Server, $"{shown} answered, but not with an Ollama model list. Check ollama_url");
        }
    }

    private static async Task<ApiReply> GetOrUnreachableAsync(
        ApiSender sender, string url, AuthenticationHeaderValue? auth, string unreachable, CancellationToken cancellationToken) {
        try {
            return await sender.SendAsync(() => {
                HttpRequestMessage request = new(HttpMethod.Get, url);
                request.Headers.Authorization = auth;
                return request;
            }, cancellationToken).ConfigureAwait(false);
        } catch (ApiException error) when (error.Kind == ApiErrorKind.Connection) {
            throw new ApiException(ApiErrorKind.Connection, unreachable);
        }
    }

    private static ApiException HostError(ApiReply reply, string label, string target) {
        ApiErrorKind kind = ApiErrors.KindOf(reply.Status, anthropic: false);
        string message = kind == ApiErrorKind.Auth
            ? ApiErrors.Message(reply.Status, null)
            : string.Create(CultureInfo.InvariantCulture, $"{label} at {target} answered HTTP {reply.Status}");
        return new ApiException(kind, message, ApiErrors.Pauses(kind) ? reply.RetryAfter : null);
    }

    private static (List<ModelInfo> Models, string? Next) ReadAnthropicPage(JsonElement root) {
        List<ModelInfo> models = [];
        foreach (JsonElement model in root.GetProperty("data").EnumerateArray()) {
            string id = model.GetProperty("id").GetString() ?? throw new FormatException("model id is null");
            string name = model.GetProperty("display_name").GetString() ?? throw new FormatException("model name is null");
            string? lifecycle = ApiReply.Field(model, "lifecycle")?.GetString();
            if (lifecycle is null || lifecycle == activeLifecycle) {
                models.Add(Priced(ProviderName.Anthropic, id, name));
            }
        }
        bool hasMore = ApiReply.Field(root, "has_more")?.GetBoolean() ?? false;
        string? last = ApiReply.Field(root, "last_id")?.GetString();
        return (models, hasMore ? last : null);
    }

    private static ModelInfo Priced(ProviderName provider, string id, string name) {
        ModelSpec? spec = Models.SpecFor(provider, id, null);
        return new ModelInfo(id, name, spec?.Input, spec?.Output, Batch: true);
    }

    private static bool IsChatModel(string id) =>
        id.StartsWith(openAIModelPrefix, StringComparison.Ordinal)
        && !excludedParts.Any(part => id.Contains(part, StringComparison.Ordinal));

    private static ModelInfo? ReadRouterModel(JsonElement entry) {
        if (entry.ValueKind != JsonValueKind.Object
            || ApiReply.Field(entry, "id") is not JsonElement { ValueKind: JsonValueKind.String } idValue
            || idValue.GetString() is not { Length: > 0 } id) {
            return null;
        }
        JsonElement? nameValue = ApiReply.Field(entry, "name");
        if (nameValue is JsonElement { ValueKind: not JsonValueKind.String }) {
            return null;
        }
        string name = nameValue?.GetString() is { Length: > 0 } given ? given : id;
        decimal? priceIn = null;
        decimal? priceOut = null;
        if (ApiReply.Field(entry, "pricing") is JsonElement { ValueKind: JsonValueKind.Object } pricing
            && PerToken(pricing, "prompt") is decimal prompt
            && PerToken(pricing, "completion") is decimal completion) {
            priceIn = PerMtok(prompt);
            priceOut = PerMtok(completion);
        }
        return new ModelInfo(id, name, priceIn, priceOut, Batch: false);
    }

    private static decimal? PerToken(JsonElement pricing, string field) =>
        Price(ApiReply.Field(pricing, field)) is decimal value && value >= 0 && value <= largestPerToken
            ? value
            : null;

    private static decimal? Price(JsonElement? field) => field switch {
        JsonElement { ValueKind: JsonValueKind.Number } number => number.TryGetDecimal(out decimal value) ? value : null,
        JsonElement { ValueKind: JsonValueKind.String } text =>
            decimal.TryParse(text.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value)
                ? value
                : null,
        _ => null,
    };

    private static decimal PerMtok(decimal perToken) => Math.Round(perToken * tokensPerMtok, priceDecimals);
}
