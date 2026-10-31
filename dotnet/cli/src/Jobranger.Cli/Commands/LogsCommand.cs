using System.Collections;
using System.CommandLine;
using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class LogsCommand : Command {
    public const string CommandName = "logs";

    public LogsCommand(CliServices services)
        : this(services, Environment.GetEnvironmentVariables()) {
    }

    public LogsCommand(CliServices services, IDictionary environment)
        : base(CommandName, services.Messages.LogsDescription) {
        SetAction(_ => Run(services, environment));
    }

    private static int Run(CliServices services, IDictionary environment) {
        ILogger logger = services.Logs.CreateLogger(typeof(LogsCommand).FullName!);
        IMessages messages = services.Messages;
        Paths paths = new(environment);
        return ReadFailures.PrintLines(
            () => Lines(paths, messages, logger),
            failure => LogEvents.LogsFailed(logger, failure),
            paths,
            messages,
            services.Console.Profile.Out.Writer);
    }

    private static IReadOnlyList<string> Lines(Paths paths, IMessages messages, ILogger logger) {
        using SqliteConnection connection = Database.Open(paths.DbPath(), logger);
        if (Runs.LastRun(connection) is not RunRow last) {
            return [messages.LogsNoRuns];
        }
        IReadOnlyList<JsonObject> entries =
            LogFiles.ReadRun(paths.LogDir(), last.Id.ToString(CultureInfo.InvariantCulture));
        return entries.Count > 0
            ? [.. entries.Select(LogFiles.FormatLine)]
            : [messages.LogsMissing(LogFiles.DefaultKeepDays)];
    }
}
