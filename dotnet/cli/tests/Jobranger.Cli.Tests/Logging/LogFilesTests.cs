using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Logging;

[UnsupportedOSPlatform("windows")]
public sealed class LogFilesTests : IDisposable {
    private const int keepDays = 14;

    private static readonly DateOnly today = new(2026, 9, 16);

    private readonly TempDir temp = new();

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldDeleteJsonlFile_WhenOlderThan14Days() {
        string[] names = [
            "2026-08-31.jsonl", "2026-09-01.jsonl", "2026-09-02.jsonl", "2026-02-30.jsonl", "2026-08-01.log", "notes.txt",
        ];
        foreach (string name in names) {
            File.WriteAllText(temp.File(name), "x");
        }

        int removed = LogFiles.Prune(temp.Path, today, keepDays);

        Assert.Equal(
            "2 2026-02-30.jsonl,2026-08-01.log,2026-09-02.jsonl,notes.txt",
            $"{removed} {string.Join(',', Directory.GetFiles(temp.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal))}");
    }

    [Fact]
    public void ShouldRemoveNothing_WhenLogDirIsMissing() {
        int removed = LogFiles.Prune(temp.File("logs"), today, keepDays);

        Assert.Equal(0, removed);
    }

    [Fact]
    public void ShouldReturnOnlyRunLines_WhenFilesHoldSeveralRuns() {
        File.WriteAllLines(temp.File("2026-09-16.jsonl"), [Line("run-1", "run.finished"), Line("run-2", "run.started")]);
        File.WriteAllLines(temp.File("2026-09-15.jsonl"), [Line("run-1", "run.started"), Line(null, "logs.pruned")]);

        IReadOnlyList<JsonObject> entries = LogFiles.ReadRun(temp.Path, "run-1");

        Assert.Equal(["run.started", "run.finished"], entries.Select(entry => (string?)entry["event"]));
    }

    [Fact]
    public void ShouldReturnNoLines_WhenLogDirIsMissing() {
        IReadOnlyList<JsonObject> entries = LogFiles.ReadRun(temp.File("logs"), "run-1");

        Assert.Empty(entries);
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[\"run-1\"]")]
    public void ShouldSkipLine_WhenLineIsNotJsonObject(string broken) {
        File.WriteAllLines(temp.File("2026-09-16.jsonl"), [broken, Line("run-1", "run.started")]);

        IReadOnlyList<JsonObject> entries = LogFiles.ReadRun(temp.Path, "run-1");

        Assert.Equal(["run.started"], entries.Select(entry => (string?)entry["event"]));
    }

    [Fact]
    public void ShouldFormatLevelEventAndAllowedFields_WhenEntryIsRead() {
        JsonObject entry = JsonNode.Parse(
            "{\"ts\":\"2026-09-16T08:00:00+02:00\",\"level\":\"info\",\"logger\":\"Jobranger.Cli\",\"event\":\"source.done\","
            + "\"run_id\":\"run-1\",\"source\":\"nav\",\"count\":2,\"cached\":true,\"trace\":[\"a.cs:1 Run\"]}")!.AsObject();

        string line = LogFiles.FormatLine(entry);

        Assert.Equal("2026-09-16T08:00:00+02:00 info source.done source=nav count=2", line);
    }

    [Fact]
    public void ShouldPrintNull_WhenFieldValueIsNull() {
        JsonObject entry = new() {
            ["ts"] = "2026-09-16T08:00:00+02:00",
            ["level"] = "info",
            ["event"] = "source.done",
            ["source"] = null,
        };

        string line = LogFiles.FormatLine(entry);

        Assert.Equal("2026-09-16T08:00:00+02:00 info source.done source=null", line);
    }

    [Fact]
    public void ShouldEscapeControlCharacters_WhenFormattingLine() {
        JsonObject entry = new() {
            ["ts"] = "2026-09-16T08:00:00+02:00",
            ["level"] = "info",
            ["event"] = "source.done",
            ["source"] = "\u001b[2J\u0007\r\n\terror forged\u202e\u2028\U000E0001",
        };

        string line = LogFiles.FormatLine(entry);

        Assert.Equal(
            "2026-09-16T08:00:00+02:00 info source.done source=\\x1b[2J\\x07\\r\\n\\terror forged\\u202e\\u2028\\U000e0001",
            line);
    }

    private static string Line(string? runId, string name) =>
        new JsonObject { ["event"] = name, ["run_id"] = runId }.ToJsonString();
}
