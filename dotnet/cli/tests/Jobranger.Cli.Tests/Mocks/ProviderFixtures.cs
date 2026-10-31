using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Tests.Mocks;

internal static class ProviderFixtures {
    public const string AnthropicKey = "sk-fake-anthropic-key-0001";
    public const string OpenAIKey = "sk-fake-openai-key-0001";
    public const string Sonnet = "claude-sonnet-5-5";
    public const string GptMini = "gpt-5-mini";
    public const string RouterModel = "vendor/model-one";
    public const string OllamaModel = "llama3.2";
    public const string OllamaRoot = "http://box:11434";
    public const string OllamaUser = "user";
    public const string OllamaPassword = "Secret42";
    public const string OllamaCredentialRoot = "http://" + OllamaUser + userSeparator + OllamaPassword + "@box:11434";
    public const string ReplyText = "{\"is_programming_job\":true}";
    public const string ErrorBody = "{\"error\":{\"message\":\"failed\"}}";
    public const string AnthropicTimeout = "Request timed out or interrupted. This could be due to a network timeout, "
        + "dropped connection, or request cancellation. "
        + "See https://docs.anthropic.com/en/api/errors#long-requests for more details.";

    private const string dataFolder = "TestData";
    private const string goldenFolder = "Golden";
    private const string providersFolder = "Providers";
    private const string requestsFolder = "Requests";
    private const string basicScheme = "Basic ";
    private const string userSeparator = ":";

    public static ClassifyPrompt Prompt { get; } = LoadPrompt();

    public static string BasicHeader(string user, string password) =>
        basicScheme + Convert.ToBase64String(Encoding.UTF8.GetBytes(user + userSeparator + password));

    public static JsonObject AnthropicSchema => Node(Golden("anthropic-schema.json")).AsObject();

    public static JsonObject OpenAISchema => Node(Golden("openai-schema.json")).AsObject();

    public static byte[] GoldenBody(string provider) => File.ReadAllBytes(GoldenPath(provider + ".json"));

    public static string AnthropicMessage(Action<JsonObject>? change = null) =>
        Reply("anthropic_message.json", reply => {
            reply["content"]![0]!["text"] = ReplyText;
            change?.Invoke(reply);
        });

    public static string Completion(string fixture, Action<JsonObject>? change = null) =>
        Reply(fixture, reply => {
            reply["choices"]![0]!["message"]!["content"] = ReplyText;
            change?.Invoke(reply);
        });

    public static ApiSender AnthropicSender(FakeApiHandler handler) =>
        new(handler, ApiClock.Create(), 0, SdkRules.Anthropic, Random.Shared);

    public static ApiSender OpenAISender(FakeApiHandler handler) =>
        new(handler, ApiClock.Create(), 0, SdkRules.OpenAI, Random.Shared);

    private static string Reply(string fixture, Action<JsonObject> change) {
        JsonObject reply = Node(File.ReadAllText(DataPath(fixture))).AsObject();
        change(reply);
        return reply.ToJsonString();
    }

    private static ClassifyPrompt LoadPrompt() {
        JsonObject fields = Node(File.ReadAllText(DataPath("prompt.json"))).AsObject();
        return new ClassifyPrompt((string)fields["system"]!, (string)fields["user"]!);
    }

    private static string Golden(string name) => File.ReadAllText(GoldenPath(name));

    private static JsonNode Node(string json) => JsonNode.Parse(json)!;

    private static string DataPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, dataFolder, providersFolder, name);

    private static string GoldenPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, goldenFolder, requestsFolder, name);
}
