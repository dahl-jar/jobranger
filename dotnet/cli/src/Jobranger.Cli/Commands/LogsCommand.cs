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
        TextWriter output = services.Console.Profile.Out.Writer;
        IMessages messages = services.Messages;
        Paths paths = new(environment);
        try {
            using SqliteConnection connection = Database.Open(paths.DbPath(), logger);
            if (Runs.LastRun(connection) is not RunRow last) {
                output.WriteLine(messages.LogsNoRuns);
                return CliExitCodes.Ok;
            }
            IReadOnlyList<JsonObject> entries =
                LogFiles.ReadRun(paths.LogDir(), last.Id.ToString(CultureInfo.InvariantCulture));
            if (entries.Count == 0) {
                output.WriteLine(messages.LogsMissing(LogFiles.DefaultKeepDays));
                return CliExitCodes.Ok;
            }
            foreach (JsonObject entry in entries) {
                output.WriteLine(LogFiles.FormatLine(entry));
            }
            return CliExitCodes.Ok;
        } catch (Exception error) when (ReadFailures.Covers(error)) {
            LogEvents.LogsFailed(logger, ReadFailures.Code(error));
            foreach (string line in ReadFailures.Lines(error, paths, messages)) {
                output.WriteLine(line);
            }
            return CliExitCodes.Failure;
        }
    }
}
