using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Setup;
using Jobranger.Cli.Files;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Mocks;

[UnsupportedOSPlatform("windows")]
internal sealed class InitHarness : IDisposable {
    private static readonly TimeSpan invokeLimit = TimeSpan.FromSeconds(30);

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly FakeTimeProvider clock = AdFixtures.Clock();

    public InitHarness() {
        Environment = CommandFixtures.Environment(temp, Locales.English);
        Paths = new Paths(Environment);
        Profile = InitFixtures.WriteProfile(temp);
    }

    public static object[] LimitAnswers => [InitFixtures.DefaultLevels, InitFixtures.DefaultYears, true, string.Empty, false];

    public static object[] WebsiteAnswers => [new[] { InitFixtures.Website }, string.Empty];

    public static object[] ModelAnswers => [InitFixtures.Anthropic, InitFixtures.ApiKey, InitFixtures.TableModel, string.Empty];

    public Hashtable Environment { get; }

    public Paths Paths { get; }

    public string Profile { get; }

    public string TempPath => temp.Path;

    public FakeTimeProvider Clock => clock;

    public string Output => console.Output;

    public string Errors => console.Errors;

    public string LastLine => console.Output.TrimEnd().Split(System.Environment.NewLine)[^1];

    public CancellationTokenSource Cancel { get; } = new();

    public FakeSecretStore Store { get; } = new();

    public FakeApiHandler ResendHandler { get; } = FakeApiHandler.Json(200, DeliveryFixtures.EmptyJson);

    public object[] YouAnswers => [Profile, new[] { InitFixtures.Role }];

    public object[] AfterModel => [.. YouAnswers, .. LimitAnswers, .. WebsiteAnswers];

    public object[] WebsiteFlow => [.. ModelAnswers, .. AfterModel];

    public object[] OllamaFlow => [InitFixtures.Ollama, string.Empty, InitFixtures.OllamaModel, .. AfterModel];

    public AppConfig Saved => ConfigFile.Load(Paths.ConfigPath(), Paths.Home());

    public static string Lines(params string[] lines) =>
        string.Concat(lines.Select(line => line + System.Environment.NewLine));

    public static FakeSmtpServer AcceptingServer() => FakeSmtpServer.Accepting(InitFixtures.StartTls, InitFixtures.AuthPlain);

    public static FakeApiHandler Listing() => FakeApiHandler.Json(200, InitFixtures.AnthropicList());

    public static FakeApiHandler OllamaListing() => FakeApiHandler.Json(200, InitFixtures.OllamaList);

    public static FakeApiHandler RouterListing() => FakeApiHandler.Json(200, InitFixtures.RouterList);

    public static FakeApiHandler RouterKeyWorks() => FakeApiHandler.Json(200, InitFixtures.RouterKeyReply);

    public static FakeApiHandler UnpricedListing() =>
        FakeApiHandler.Json(200, InitFixtures.AnthropicList(InitFixtures.UnpricedModel, InitFixtures.UnpricedModelName));

    public static string ServerAnswer(FakeSmtpServer server) => $"127.0.0.1:{server.Port}";

    public void Dispose() {
        Cancel.Dispose();
        console.Dispose();
        temp.Dispose();
    }

    public void SetLanguage(string language) => Environment["LANG"] = language;

    public object[] SmtpFlow(FakeSmtpServer server) =>
        [.. ModelAnswers, .. YouAnswers, .. LimitAnswers, new[] { InitFixtures.Website, InitFixtures.Email }, DeliveryFixtures.Address,
        InitFixtures.Smtp, ServerAnswer(server), string.Empty, ConfigFixtures.Secret, string.Empty];

    public object[] ResendFlow() =>
        [.. ModelAnswers, .. YouAnswers, .. LimitAnswers, new[] { InitFixtures.Email }, DeliveryFixtures.Address,
        InitFixtures.Resend, DeliveryFixtures.ResendKey, string.Empty, string.Empty];

    public ScriptedPrompter Prompter(params object[] answers) =>
        new(console.Console.Profile.Out.Writer, answers) { OnCancel = Cancel.Cancel };

    public InitSeams Seams(IPrompter? prompter, params FakeApiHandler[] handlers) =>
        InitFixtures.Seams(prompter, Store, handlers, SmtpCertificates.Trusting with { CreateResendHandler = () => ResendHandler });

    public Task<int> InvokeAfterRefusalAsync(ScriptedPrompter prompter) =>
        InvokeAsync(Seams(prompter, FakeApiHandler.Refusing(), RouterListing(), RouterKeyWorks()));

    public byte[] WriteOldConfig(string text) {
        Directory.CreateDirectory(Paths.ConfigDir());
        File.WriteAllText(Paths.ConfigPath(), text);
        return File.ReadAllBytes(Paths.ConfigPath());
    }

    public string TempFile(string name) => temp.File(name);

    public SetupContext Context(IPrompter prompter, params FakeApiHandler[] handlers) {
        InitSeams seams = Seams(prompter, handlers);
        return new SetupContext(
            prompter,
            console.Console.Profile.Out.Writer,
            new EnglishMessages(),
            Paths,
            Environment,
            clock,
            new SetupSeams(seams.CreateModelHandler, seams.OpenSecrets, (_, _, _) => Task.FromResult<string?>(null)),
            NullLogger.Instance);
    }

    public Task<int> InvokeAsync(InitSeams seams, bool inputRedirected = false) {
        CliServices services = CliServices.Create(clock, console.Console, console.ErrorConsole, Environment);
        System.CommandLine.RootCommand root = [new InitCommand(services, Environment, clock, inputRedirected, seams)];
        return root.Parse([InitCommand.CommandName]).InvokeAsync(null, Cancel.Token)
            .WaitAsync(invokeLimit, TestContext.Current.CancellationToken);
    }
}
