using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Logging;

[UnsupportedOSPlatform("windows")]
internal sealed class JsonLoggerProvider(JsonLogWriter writer, TimeProvider time) : ILoggerProvider {
    private const string timestampFormat = "yyyy-MM-dd'T'HH:mm:sszzz";
    private const string unknown = "?";
    private const string stateMachineMethod = "MoveNext";
    private const char generatedNameStart = '<';
    private const char generatedNameEnd = '>';

    public ILogger CreateLogger(string categoryName) => new JsonLogger(this, categoryName);

    public void Dispose() {
    }

    private static string LevelName(LogLevel level) => level switch {
        LogLevel.Warning => "warning",
        LogLevel.Error => "error",
        LogLevel.Critical => "critical",
        _ => "info",
    };

    private static JsonArray Frames(Exception error) {
        StackFrame[] frames = new StackTrace(error, fNeedFileInfo: true).GetFrames();
        return new JsonArray([.. frames.Reverse().Select(frame => JsonValue.Create(Frame(frame)))]);
    }

    private static string Frame(StackFrame frame) {
        string file = frame.GetFileName() is string path ? Path.GetFileName(path) : unknown;
        return string.Create(
            CultureInfo.InvariantCulture, $"{file}:{frame.GetFileLineNumber()} {MethodName(frame)}");
    }

    private static string MethodName(StackFrame frame) {
        DiagnosticMethodInfo? method = DiagnosticMethodInfo.Create(frame);
        if (method is null) {
            return unknown;
        }
        string type = method.DeclaringTypeName;
        int start = type.LastIndexOf(generatedNameStart);
        int end = type.LastIndexOf(generatedNameEnd);
        return method.Name == stateMachineMethod && start >= 0 && end > start ? type[(start + 1)..end] : method.Name;
    }

    private void Write<TState>(string category, LogLevel level, EventId eventId, TState state, Exception? error) {
        DateTimeOffset now = time.GetLocalNow();
        JsonObject entry = new() {
            [LogFields.Timestamp] = now.ToString(timestampFormat, CultureInfo.InvariantCulture),
            [LogFields.Level] = LevelName(level),
            [LogFields.Logger] = category,
            [LogFields.Event] = LogFields.EventName(eventId.Name),
            [LogFields.RunId] = RunScope.Current,
        };
        if (state is IReadOnlyList<KeyValuePair<string, object?>> fields) {
            foreach (KeyValuePair<string, object?> field in fields) {
                string name = LogFields.FieldName(field.Key);
                if (LogFields.Allowed.Contains(name)) {
                    entry[name] = LogFields.Scalar(field.Value);
                }
            }
        }
        if (error is not null) {
            entry[LogFields.ErrorType] = LogFields.Cut(error.GetType().Name);
            entry[LogFields.Trace] = Frames(error);
        }
        writer.Write(entry, DateOnly.FromDateTime(now.DateTime));
    }

    private sealed class JsonLogger(JsonLoggerProvider provider, string category) : ILogger {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel is >= LogLevel.Information and < LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (IsEnabled(logLevel)) {
                provider.Write(category, logLevel, eventId, state, exception);
            }
        }
    }
}
