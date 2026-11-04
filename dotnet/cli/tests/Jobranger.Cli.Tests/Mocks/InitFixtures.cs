using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Tests.Mocks;

[UnsupportedOSPlatform("windows")]
internal static class InitFixtures {
    public const string ApiKey = "sk-fake-key-1234";
    public const string OtherKey = "sk-fake-key-5678";
    public const string TableModel = "claude-sonnet-5-5";
    public const string TableModelName = "Claude Sonnet";
    public const string UnpricedModel = "claude-unlisted";
    public const string UnpricedModelName = "Claude Unlisted";
    public const string RouterModel = "vendor/model-one";
    public const string OllamaModel = "llama3";
    public const string Anthropic = "anthropic";
    public const string OpenRouter = "openrouter";
    public const string Ollama = "ollama";
    public const string Role = "backend";
    public const string Smtp = "smtp";
    public const string Resend = "resend";
    public const string Website = "website";
    public const string Email = "email";
    public const string OtherPassword = "pass-5678";
    public const string AuthPlain = "AUTH PLAIN";
    public const string StartTls = "STARTTLS";
    public const string LoginRefused = "535 no\r\n";
    public const string OldConfig = "version: 1\n";
    public const string ProfileBody = "summary: s\nprofessional_years: 1\n";
    public const string EmptyOllamaList = """{"models":[]}""";
    public const string OllamaList = """{"models":[{"name":"llama3"}]}""";
    public const string AuthRefused = """{"error":{"type":"authentication_error","message":"bad key"}}""";
    public const string RouterKeyReply = """{"data":{"label":"key"}}""";
    public const string RouterList =
        """{"data":[{"id":"vendor/model-one","name":"Model One","pricing":{"prompt":"0.00001","completion":"0.00002"}}]}""";

    private const string profileFile = "profile.yaml";

    public static string[] DefaultLevels => ["junior", "mid", "none"];

    public static string[] DefaultYears => ["0-1", "1-3"];

    public static string AnthropicList(string id = TableModel, string name = TableModelName) =>
        $$"""{"data":[{"id":"{{id}}","display_name":"{{name}}"}],"has_more":false}""";

    public static string WriteProfile(TempDir temp) {
        string path = temp.File(profileFile);
        File.WriteAllText(path, ProfileBody);
        return path;
    }

    public static InitSeams Seams(
        IPrompter? prompter,
        ISecretStore? store = null,
        IEnumerable<HttpMessageHandler>? handlers = null,
        DeliveryTransport? transport = null) {
        Queue<HttpMessageHandler> queue = new(handlers ?? []);
        return new InitSeams(
            (_, _) => prompter,
            _ => queue.Dequeue(),
            (_, _) => Task.FromResult(store),
            transport ?? SmtpCertificates.Trusting);
    }
}
