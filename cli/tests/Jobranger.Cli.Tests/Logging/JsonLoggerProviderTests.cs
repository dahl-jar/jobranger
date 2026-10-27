using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Logging;

[UnsupportedOSPlatform("windows")]
public sealed class JsonLoggerProviderTests : IDisposable {
    private const string category = "Jobranger.Cli.Tests.Logging";
    private const string leakedText = "Senior developer wanted, CV attached";
    private const string invalidEvent = "invalid_event";
    private const int maxChars = 64;
    private const string localDayFile = "2026-10-13.jsonl";
    private const string nextDayFile = "2026-10-14.jsonl";
    private const string localTimestamp = "2026-10-13T01:30:00+02:00";
    private const string zoneName = "plus-two";

    private static readonly DateTimeOffset utcEvening = new(2026, 10, 12, 23, 30, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo plusTwo =
        TimeZoneInfo.CreateCustomTimeZone(zoneName, TimeSpan.FromHours(2), zoneName, zoneName);

    private readonly TempDir temp = new();
    private readonly FakeTimeProvider time = AfterLocalMidnight();
    private readonly JsonLoggerProvider provider;

    public JsonLoggerProviderTests() {
        provider = new JsonLoggerProvider(new JsonLogWriter(temp.Path), time);
    }

    public static TheoryData<object?, string> SafeValues => new() {
        { "nav", "\"nav\"" },
        { new string('x', 100), "\"" + new string('x', maxChars) + "\"" },
        { double.NaN, "\"nan\"" },
        { double.PositiveInfinity, "\"inf\"" },
        { double.NegativeInfinity, "\"-inf\"" },
        { 2.5, "2.5" },
        { 3, "3" },
        { 3L, "3" },
        { 1.25m, "1.25" },
        { true, "true" },
        { new Uri("https://example.test"), "null" },
    };

    public void Dispose() {
        provider.Dispose();
        temp.Dispose();
    }

    [Fact]
    public void ShouldWriteBaseFields_WhenEventHasNoFields() {
        Log("run.started");

        JsonObject entry = Assert.Single(Lines());
        Assert.Equal(
            ("ts,level,logger,event,run_id", localTimestamp, "info", category),
            (string.Join(',', entry.Select(field => field.Key)),
                (string?)entry["ts"], (string?)entry["level"], (string?)entry["logger"]));
    }

    [Fact]
    public void ShouldWriteLineToLocalDayFile_WhenUtcDateDiffers() {
        Log("run.started");

        Assert.Equal([localDayFile], Directory.GetFiles(temp.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void ShouldWriteToNextDayFile_WhenDateChanges() {
        Log("run.started");
        time.Advance(TimeSpan.FromDays(1));

        Log("run.finished");

        Assert.Equal("run.finished", (string?)Assert.Single(LogLines.Read(temp.File(nextDayFile)))["event"]);
    }

    [Theory]
    [InlineData(LogLevel.Warning, "warning")]
    [InlineData(LogLevel.Error, "error")]
    [InlineData(LogLevel.Critical, "critical")]
    public void ShouldWriteLevelName_WhenLevelIsAboveInformation(LogLevel level, string expected) {
        LogAt(level, "run.failed", null);

        Assert.Equal(expected, (string?)Assert.Single(Lines())["level"]);
    }

    [Theory]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.None)]
    public void ShouldSkipLine_WhenLevelIsOutsideInformationToCritical(LogLevel level) {
        LogAt(level, "run.started", null);

        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Fact]
    public void ShouldDropField_WhenNotInAllowlist() {
        Log("source.done", ("source", "nav"), ("cv_text", leakedText));

        JsonObject entry = Assert.Single(Lines());
        Assert.Equal(("nav", false), ((string?)entry["source"], entry.ContainsKey("cv_text")));
    }

    [Fact]
    public void ShouldWriteSnakeCaseField_WhenPlaceholderIsPascalCase() {
        Log("classify.done", ("InputTokens", 120));

        Assert.Equal(120, (int?)Assert.Single(Lines())["input_tokens"]);
    }

    [Theory]
    [MemberData(nameof(SafeValues))]
    public void ShouldWriteSafeValue_WhenFieldIsAllowed(object? value, string expected) {
        Log("source.done", ("source", value));

        JsonNode? written = Assert.Single(Lines())["source"];
        Assert.Equal(JsonNode.Parse(expected)?.ToJsonString() ?? "null", written?.ToJsonString() ?? "null");
    }

    [Fact]
    public void ShouldLeaveMessageOut_WhenEventHasFormatArgs() {
        Log("classify.chunk.unreadable", ("{OriginalFormat}", "output={Output}"), ("Output", leakedText));

        Assert.DoesNotContain(leakedText, File.ReadAllText(temp.File(localDayFile)), StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldLogTypeWithoutMessage_WhenExceptionIsLogged() {
        LogException(CaughtException());

        string line = File.ReadAllText(temp.File(localDayFile));
        Assert.Equal(
            ("InvalidOperationException", false),
            ((string?)Assert.Single(Lines())["error_type"], line.Contains(leakedText, StringComparison.Ordinal)));
    }

    [Fact]
    public void ShouldWriteThrowingMethodLast_WhenExceptionIsLogged() {
        LogException(CaughtException());

        JsonArray trace = Assert.Single(Lines())["trace"]!.AsArray();
        Assert.Matches(@"^JsonLoggerProviderTests\.cs:\d+ ThrowWithSecret$", (string?)trace[^1]);
    }

    [Fact]
    public async Task ShouldWriteAsyncMethodLast_WhenAsyncMethodThrows() {
        LogException(await CaughtAsyncException());

        JsonArray trace = Assert.Single(Lines())["trace"]!.AsArray();
        Assert.EndsWith(" " + nameof(ThrowWithSecretAsync), (string?)trace[^1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("run")]
    [InlineData("Run.started")]
    [InlineData("run.started\n")]
    [InlineData("run.xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    [InlineData("run.Started")]
    [InlineData("run.started rejected")]
    public void ShouldWriteInvalidEvent_WhenEventNameBreaksPattern(string? name) {
        Log(name);

        Assert.Equal(invalidEvent, (string?)Assert.Single(Lines())["event"]);
    }

    [Fact]
    public void ShouldKeepEvent_WhenNameIsAtLengthLimit() {
        string name = "run." + new string('x', maxChars - 4);

        Log(name);

        Assert.Equal(name, (string?)Assert.Single(Lines())["event"]);
    }

    [Fact]
    public async Task ShouldTagLineWithRunId_WhenLoggedFromSourceTask() {
        using (RunScope.Bind("run-1")) {
            await Task.Factory.StartNew(
                () => Log("source.done"),
                TestContext.Current.CancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        Assert.Equal("run-1", (string?)Assert.Single(Lines())["run_id"]);
    }

    private static FakeTimeProvider AfterLocalMidnight() {
        FakeTimeProvider clock = new(utcEvening);
        clock.SetLocalTimeZone(plusTwo);
        return clock;
    }

    private static string Render<TState>(TState state, Exception? error) => leakedText;

    private static InvalidOperationException CaughtException() {
        try {
            ThrowWithSecret();
        } catch (InvalidOperationException error) {
            return error;
        }
        throw new InvalidOperationException("ThrowWithSecret returned without throwing");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowWithSecret() => throw new InvalidOperationException(leakedText);

    private static async Task<InvalidOperationException> CaughtAsyncException() {
        try {
            await ThrowWithSecretAsync();
        } catch (InvalidOperationException error) {
            return error;
        }
        throw new InvalidOperationException("ThrowWithSecretAsync returned without throwing");
    }

    private static async Task ThrowWithSecretAsync() {
        await Task.Yield();
        throw new InvalidOperationException(leakedText);
    }

    private void LogException(Exception error) => LogAt(LogLevel.Error, "source.failed", error);

    private void Log(string? name, params (string Key, object? Value)[] fields) =>
        LogAt(LogLevel.Information, name, null, fields);

    private void LogAt(LogLevel level, string? name, Exception? error, params (string Key, object? Value)[] fields) {
        IReadOnlyList<KeyValuePair<string, object?>> state = LogLines.Fields(fields);
        provider.CreateLogger(category).Log(level, new EventId(1, name), state, error, Render);
    }

    private IReadOnlyList<JsonObject> Lines() => LogLines.Read(temp.File(localDayFile));
}
