using System.Text.Json;

namespace Jobranger.Cli.Http;

internal static class ProviderHosts {
    public const string AnthropicUrl = "https://api.anthropic.com";
    public const string AnthropicKeyHeader = "x-api-key";
    public const string AnthropicVersionHeader = "anthropic-version";
    public const string AnthropicVersion = "2023-06-01";
    public const string OpenAIUrl = "https://api.openai.com/v1";
    public const string OpenRouterUrl = "https://openrouter.ai/api/v1";
    public const string Bearer = "Bearer";

    public static string[] ReadOllamaTags(JsonElement root) =>
        [.. root.GetProperty("models").EnumerateArray()
            .Select(tag => tag.GetProperty("name").GetString() ?? throw new FormatException("tag name is null"))];
}
