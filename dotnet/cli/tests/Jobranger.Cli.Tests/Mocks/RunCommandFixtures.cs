using System.Collections;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;

namespace Jobranger.Cli.Tests.Mocks;

[UnsupportedOSPlatform("windows")]
internal static class RunCommandFixtures {
    public const string CancelledRow = "cancelled|1";
    public const int Interrupted = 130;

    private const string ollamaModel = "llama3";
    private const string profileFile = "profile_user.yaml";
    private const string resendVariable = "RESEND_API_KEY";

    public static Hashtable Environment(TempDir temp, string lang) => new() {
        ["HOME"] = temp.Path,
        ["LANG"] = lang,
        ["JOBRANGER_CONFIG_DIR"] = Path.Combine(temp.Path, "config"),
        ["JOBRANGER_DATA_DIR"] = Path.Combine(temp.Path, "data"),
        ["JOBRANGER_STATE_DIR"] = Path.Combine(temp.Path, "state"),
        ["JOBRANGER_SECRET_STORE"] = "file",
        [resendVariable] = DeliveryFixtures.ResendKey,
    };

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
            .SelectMany(file => File.ReadLines(file.FullName))
            .Select(line => JsonNode.Parse(line)!.AsObject()),
    ];
}
