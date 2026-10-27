using Jobranger.Cli.Config;
using Jobranger.Cli.Http;
using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Features.Models;

internal sealed class Models(Func<HttpMessageHandler> createHandler, TimeProvider time) {
    public static ModelSpec? SpecFor(ProviderName provider, string modelId, PriceOverride? price) =>
        Prices.SpecFor(ModelCatalog.Embedded, provider, modelId, price);

    public async Task<IReadOnlyList<ModelInfo>> ListAsync(
        ProviderName provider, string? key, string ollamaUrl, CancellationToken cancellationToken) {
        List<ModelInfo> models = provider switch {
            ProviderName.Anthropic or ProviderName.OpenAI => await ListKeyedAsync(provider, key, cancellationToken)
                .ConfigureAwait(false),
            ProviderName.OpenRouter => await ListOpenRouterAsync(cancellationToken).ConfigureAwait(false),
            ProviderName.Ollama => await ListOllamaAsync(ollamaUrl, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "provider has no model list"),
        };
        return [.. models.OrderBy(model => model.PriceIn is null).ThenBy(model => model.PriceIn ?? 0m)];
    }

    private static string RequireKey(ProviderName provider, string? key) {
        string label = provider.ToString();
        if (string.IsNullOrWhiteSpace(key)) {
            throw new ApiException(ApiErrorKind.Auth, ApiErrors.MissingKey(label));
        }
        try {
            return ApiKeys.Require(label, key);
        } catch (ArgumentException error) {
            throw new ApiException(ApiErrorKind.Auth, error.Message);
        }
    }

    private async Task<List<ModelInfo>> ListKeyedAsync(
        ProviderName provider, string? key, CancellationToken cancellationToken) {
        string checkedKey = RequireKey(provider, key);
        SdkRules rules = provider == ProviderName.Anthropic ? SdkRules.Anthropic : SdkRules.OpenAI;
        using ApiSender sender = new(createHandler(), time, ApiSender.DefaultRetries, rules, Random.Shared);
        return provider == ProviderName.Anthropic
            ? await ModelLists.AnthropicAsync(sender, checkedKey, cancellationToken).ConfigureAwait(false)
            : await ModelLists.OpenAIAsync(sender, checkedKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<ModelInfo>> ListOpenRouterAsync(CancellationToken cancellationToken) {
        using ApiSender sender = new(createHandler(), time, 0, SdkRules.OpenAI, Random.Shared);
        return await ModelLists.OpenRouterAsync(sender, cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<ModelInfo>> ListOllamaAsync(string ollamaUrl, CancellationToken cancellationToken) {
        string root = OllamaUrls.CheckedRoot(ollamaUrl);
        using ApiSender sender = new(createHandler(), time, 0, SdkRules.OpenAI, Random.Shared);
        return await ModelLists.OllamaAsync(sender, root, cancellationToken).ConfigureAwait(false);
    }
}
