using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Updates;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Jobranger.Cli;

internal sealed class CliServices(
    IAnsiConsole console,
    IAnsiConsole errorConsole,
    IMessages messages,
    ILoggerProvider logs,
    Updates updates) {
    public IAnsiConsole Console { get; } = console;

    public IAnsiConsole ErrorConsole { get; } = errorConsole;

    public IMessages Messages { get; } = messages;

    public ILoggerProvider Logs { get; } = logs;

    public Updates Updates { get; } = updates;

    [UnsupportedOSPlatform("windows")]
    public static CliServices Create(
        TimeProvider time, IAnsiConsole console, IAnsiConsole errorConsole, IDictionary environment) {
        Paths paths = new(environment);
        IMessages messages = Text.Messages.For(ConfigFile.ReadLanguage(paths.ConfigPath()), environment);
        JsonLoggerProvider logs = new(new JsonLogWriter(paths.LogDir()), time);
        PruneLogs(paths.LogDir(), time, logs);
        Updates updates = new(
            errorConsole,
            messages,
            environment,
            paths.VersionCheckPath(),
            Environment.ProcessPath,
            AppVersion.Text,
            time,
            () => new SocketsHttpHandler());
        return new(console, errorConsole, messages, logs, updates);
    }

    [UnsupportedOSPlatform("windows")]
    private static void PruneLogs(string logDir, TimeProvider time, JsonLoggerProvider logs) {
        try {
            int removed = LogFiles.Prune(logDir, DateOnly.FromDateTime(time.GetLocalNow().DateTime));
            if (removed > 0) {
                ILogger logger = logs.CreateLogger(typeof(LogFiles).FullName!);
                LogEvents.LogsPruned(logger, removed, LogFiles.DefaultKeepDays);
            }
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return;
        }
    }
}
