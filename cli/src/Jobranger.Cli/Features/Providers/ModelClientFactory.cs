using Jobranger.Cli.Config;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Providers;

internal static class ModelClientFactory {
    public static IModelClient Create(
        ProviderName provider, string model, string key, string ollamaRoot, bool supportsEffort, ApiSender sender) =>
        provider == ProviderName.Anthropic
            ? new AnthropicClient(sender, key, model, supportsEffort)
            : new OpenAICompatClient(sender, provider, key, model, ollamaRoot, supportsEffort);
}
