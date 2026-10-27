using Jobranger.Cli.Config;
using Jobranger.Cli.Http;
using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Features.Providers;

internal sealed class Providers(Func<HttpMessageHandler> createHandler, TimeProvider time) {
    private const string noModel = "no model chosen, run init to pick a provider and model";

    private static readonly Dictionary<ProviderName, string> secretNames = new() {
        [ProviderName.Anthropic] = "anthropic_api_key",
        [ProviderName.OpenAI] = "openai_api_key",
        [ProviderName.OpenRouter] = "openrouter_api_key",
    };

    public async Task<IModelClient> CreateAsync(
        ModelConfig model,
        Func<string, CancellationToken, Task<string?>> secrets,
        bool supportsEffort,
        CancellationToken cancellationToken) {
        if (model.Provider is not ProviderName provider || model.Model is not string modelId) {
            throw new ModelSetupException(noModel);
        }
        string key = await KeyAsync(provider, secrets, cancellationToken).ConfigureAwait(false);
        string ollamaRoot = provider == ProviderName.Ollama ? OllamaRoot(model.OllamaUrl) : string.Empty;
        SdkRules rules = provider == ProviderName.Anthropic ? SdkRules.Anthropic : SdkRules.OpenAI;
        ApiSender sender = new(createHandler(), time, ApiSender.DefaultRetries, rules, Random.Shared);
        return ModelClientFactory.Create(provider, modelId, key, ollamaRoot, supportsEffort, sender);
    }

    private static async Task<string> KeyAsync(
        ProviderName provider, Func<string, CancellationToken, Task<string?>> secrets, CancellationToken cancellationToken) {
        if (provider == ProviderName.Ollama) {
            return OpenAICompatClient.OllamaKey;
        }
        if (!secretNames.TryGetValue(provider, out string? name)) {
            throw new ArgumentOutOfRangeException(nameof(provider), provider, "provider has no saved key name");
        }
        string label = provider.ToString();
        string? saved = await secrets(name, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(saved)) {
            throw new ModelSetupException(ApiErrors.MissingKey(label));
        }
        try {
            return ApiKeys.Require(label, saved);
        } catch (ArgumentException error) {
            throw new ModelSetupException(error.Message);
        }
    }

    private static string OllamaRoot(string url) {
        try {
            return OllamaUrls.CheckedRoot(url);
        } catch (ApiException error) {
            throw new ModelSetupException(error.Message);
        }
    }
}
