using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Ads;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;

namespace Jobranger.Cli.Tests.Mocks;

[UnsupportedOSPlatform("windows")]
internal static class EditFixtures {
    public const string NoModel = "model:\n  provider: null\n";
    public const string PricedModel =
        "model:\n  provider: anthropic\n  model: test-model\n  price:\n    input_per_mtok: 1\n    output_per_mtok: 5\n";
    public const string UnpricedModel = "model:\n  provider: anthropic\n  model: test-model\n  price: null\n";
    public const UnixFileMode ReadOnlyDir = UnixFileMode.UserRead | UnixFileMode.UserExecute;
    public const string EditAgain = "Edit again? (Y/n) ";
    public const string EditAgainNorwegian = "Endre igjen? (J/n) ";
    public const string BrokenContext = "- about\n";
    public const string BrokenContextLine = "context.yaml: is not a section of settings\n";
    public const string UnknownSetting = "nope: 1\n";

    private const string modelSection = "model:\n";
    private const string notifySection = "notify:\n";
    private const string profileFileName = "profile.yaml";
    private const string profileFacts = "x";

    public static string Config(string model) {
        string text = File.ReadAllText(ConfigFixtures.ConfigTestData("default.yaml"));
        int start = text.IndexOf(modelSection, StringComparison.Ordinal);
        int end = text.IndexOf(notifySection, StringComparison.Ordinal);
        return text[..start] + model + text[end..];
    }

    public static PromptSetup CurrentPrompt(Paths paths) =>
        PromptSetup.Load(ConfigFile.Load(paths.ConfigPath(), paths.Home()), paths);

    public static void WriteConfig(Paths paths, string text) {
        Directory.CreateDirectory(paths.ConfigDir());
        File.WriteAllText(paths.ConfigPath(), text);
    }

    public static void WriteSetup(TempDir temp, Paths paths, string model) {
        WriteConfig(paths, Config(model));
        File.WriteAllText(temp.File(profileFileName), ProfileFixtures.FactsProfile + profileFacts);
    }

    public static void AddAds(Paths paths, int count, string version) {
        using SqliteConnection connection = Database.Open(paths.DbPath(), NullLogger.Instance);
        AdsEntry ads = new(connection, AdFixtures.Clock());
        foreach (int index in Enumerable.Range(1, count)) {
            ads.Upsert(AdFixtures.Numbered(index) with { AssessmentVersion = version });
        }
    }

    public static FakeEditorProcess Writes(string text) => new(file => {
        File.WriteAllText(file, text);
        return 0;
    });

    public static FakeEditorProcess Exits(int code) => new(_ => code);

    public static EditServices EditServices(IDictionary environment, IEditorProcess editor, params string?[] answers) {
        Queue<string?> pending = new(answers);
        return new EditServices(
            environment, AdFixtures.Clock(), editor, () => pending.Count > 0 ? pending.Dequeue() : null);
    }

    public static CliServices Services(IDictionary environment, RecordingConsole console) =>
        CliServices.Create(AdFixtures.Clock(), console.Console, console.ErrorConsole, environment);

    public static Task<int> InvokeAsync(Command group, RecordingConsole console, params string[] args) {
        System.CommandLine.RootCommand root = [group];
        InvocationConfiguration configuration = new() { Error = console.ErrorConsole.Profile.Out.Writer };
        return root.Parse(args).InvokeAsync(configuration, TestContext.Current.CancellationToken);
    }
}
