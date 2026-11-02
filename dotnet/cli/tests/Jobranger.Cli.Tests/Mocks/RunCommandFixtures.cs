using System.Collections;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;

namespace Jobranger.Cli.Tests.Mocks;

[UnsupportedOSPlatform("windows")]
internal static class RunCommandFixtures {
    public const string CancelledRow = "cancelled|1";
    public const int Interrupted = 130;
    public const string CountsEnglish = "0 ads read, 0 new, 0 matches.";
    public const string CountsNorwegian = "0 annonser lest, 0 nye, 0 treff.";
    public const string SavedEnglish = "✓ Saved for the website";
    public const string SavedNorwegian = "✓ Lagret for nettsiden";
    public const string SkipLine = "AI checks skipped: no model or key. Set one with: jobranger init";

    public static readonly TimeSpan InvokeLimit = TimeSpan.FromSeconds(30);

    private const string ollamaModel = "llama3";
    private const string profileFile = "profile_user.yaml";
    private const string resendVariable = "RESEND_API_KEY";

    public static string Lines(params string[] lines) => string.Join(System.Environment.NewLine, lines);

    public static Task<int> InvokeAsync(
        Hashtable environment,
        RecordingConsole console,
        RunSeams seams,
        TimeProvider time,
        CancellationToken cancellationToken,
        bool inputRedirected = true,
        IAnsiConsole? output = null) {
        CliServices services = CliServices.Create(time, output ?? console.Console, console.ErrorConsole, environment);
        System.CommandLine.RootCommand root = [new RunCommand(services, environment, time, inputRedirected, seams)];
        return root.Parse([RunCommand.CommandName]).InvokeAsync(null, cancellationToken)
            .WaitAsync(InvokeLimit, TestContext.Current.CancellationToken);
    }

    public static Hashtable Environment(TempDir temp, string language) {
        Hashtable environment = CommandFixtures.Environment(temp, language);
        environment[resendVariable] = DeliveryFixtures.ResendKey;
        return environment;
    }

    public static void Save(Hashtable environment, AppConfig config) {
        Paths paths = new(environment);
        ConfigFile.Save(paths.ConfigPath(), config, paths.Home(), NullLogger.Instance);
    }

    public static AppConfig Config(EmailConfig? email = null, bool website = true, ModelConfig? model = null) {
        AppConfig config = RunFixtures.Config(MatchingFixtures.TestData(profileFile));
        return config with {
            Notify = new NotifyConfig { Website = website, Email = email ?? new EmailConfig() },
            Model = model ?? config.Model,
        };
    }

    public static ModelConfig Ollama(int port) => new() {
        Provider = ProviderName.Ollama,
        Model = ollamaModel,
        OllamaUrl = $"http://127.0.0.1:{port}",
    };

    public static List<string> RunRows(Hashtable environment) {
        using SqliteConnection database = TestDatabase.OpenRaw(new Paths(environment).DbPath());
        return TestDatabase.Rows(database, RunFixtures.RunRowsQuery);
    }

    public static void SeedPendingAd(Hashtable environment, TimeProvider time) {
        using SqliteConnection database = Database.Open(new Paths(environment).DbPath(), NullLogger.Instance);
        new AdsEntry(database, time).Upsert(RunFixtures.Pending(1, "Utvikler", "Org One AS"));
    }

    public static List<JsonObject> LogEntries(Hashtable environment) => [
        .. Directory.CreateDirectory(new Paths(environment).LogDir()).EnumerateFiles("*" + JsonLogWriter.FileSuffix)
            .SelectMany(file => LogLines.Read(file.FullName)),
    ];
}
