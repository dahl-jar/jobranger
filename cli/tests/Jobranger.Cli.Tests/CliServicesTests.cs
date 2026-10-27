using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests;

[UnsupportedOSPlatform("windows")]
public sealed class CliServicesTests : IDisposable {
    private const string stateDirVariable = "JOBRANGER_STATE_DIR";
    private const string todayFile = "2026-09-16.jsonl";
    private const string oldFile = "2026-08-01.jsonl";

    private readonly TempDir temp = new();
    private readonly RecordingConsole console = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 9, 16, 8, 0, 0, TimeSpan.Zero));

    public void Dispose() {
        console.Dispose();
        temp.Dispose();
    }

    [Fact]
    public void ShouldUseConfigLanguage_WhenConfigSetsIt() {
        File.WriteAllText(temp.File("config.yaml"), "language: nb\n");

        CliServices services = Create(Locales.English);

        Assert.IsType<NorwegianMessages>(services.Messages);
    }

    [Fact]
    public void ShouldUseLocaleLanguage_WhenConfigIsMissing() {
        CliServices services = Create(Locales.Norwegian);

        Assert.IsType<NorwegianMessages>(services.Messages);
    }

    [Fact]
    public void ShouldUseLocaleLanguage_WhenConfigIsNotYaml() {
        File.WriteAllText(temp.File("config.yaml"), "language: en\nname: a: b\n");

        CliServices services = Create(Locales.Norwegian);

        Assert.IsType<NorwegianMessages>(services.Messages);
    }

    [Fact]
    public void ShouldUseLocaleLanguage_WhenConfigIsUnreadable() {
        string path = temp.File("config.yaml");
        File.WriteAllText(path, "language: en\n");
        File.SetUnixFileMode(path, UnixFileMode.None);

        CliServices services = Create(Locales.Norwegian);

        Assert.IsType<NorwegianMessages>(services.Messages);
    }

    [Fact]
    public void ShouldWriteLogLineUnderStateDir_WhenServicesLog() {
        CliServices services = Create(Locales.English);

        IReadOnlyList<KeyValuePair<string, object?>> state = LogLines.Fields();
        services.Logs.CreateLogger("Jobranger.Cli").Log(
            LogLevel.Information, new EventId(1, "run.started"), state, null, (fields, error) => "");

        Assert.Equal("run.started", (string?)Assert.Single(LogLines.Read(LogFile(todayFile)))["event"]);
    }

    [Fact]
    public void ShouldLogPrunedCount_WhenOldLogsAreDeleted() {
        Directory.CreateDirectory(LogFile(""));
        File.WriteAllText(LogFile(oldFile), "");

        Create(Locales.English);

        Assert.Equal(
            ("logs.pruned", 1, 14),
            Pruned(Assert.Single(LogLines.Read(LogFile(todayFile)))));
    }

    [Fact]
    public void ShouldCreateServices_WhenLogDirIsUnreadable() {
        Directory.CreateDirectory(LogFile(""));
        File.SetUnixFileMode(LogFile(""), UnixFileMode.None);

        CliServices services = Create(Locales.Norwegian);

        File.SetUnixFileMode(LogFile(""), FileModes.OwnerOnlyDir);
        Assert.IsType<NorwegianMessages>(services.Messages);
    }

    private static (string?, int?, int?) Pruned(JsonObject entry) =>
        ((string?)entry["event"], (int?)entry["removed"], (int?)entry["keep_days"]);

    private string LogFile(string name) => Path.TrimEndingDirectorySeparator(Path.Combine(temp.Path, "state", "logs", name));

    private CliServices Create(string lang) => CliServices.Create(
        time,
        console.Console,
        console.Console,
        new Dictionary<string, string> {
            [Locales.LangVariable] = lang,
            [Locales.ConfigDirVariable] = temp.Path,
            [stateDirVariable] = Path.Combine(temp.Path, "state"),
        });
}
