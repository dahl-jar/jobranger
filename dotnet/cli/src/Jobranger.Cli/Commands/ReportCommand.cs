using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class ReportCommand : Command {
    public const string CommandName = "report";

    public ReportCommand(CliServices services)
        : this(services, Environment.GetEnvironmentVariables(), TimeProvider.System) {
    }

    public ReportCommand(CliServices services, IDictionary environment, TimeProvider time)
        : base(CommandName, services.Messages.ReportDescription) {
        SetAction(_ => Run(services, environment, time));
    }

    private static int Run(CliServices services, IDictionary environment, TimeProvider time) {
        ILogger logger = services.Logs.CreateLogger(typeof(ReportCommand).FullName!);
        TextWriter output = services.Console.Profile.Out.Writer;
        IMessages messages = services.Messages;
        Paths paths = new(environment);
        try {
            using SqliteConnection connection = Database.Open(paths.DbPath(), logger);
            if (Runs.LastRun(connection) is not RunRow last) {
                output.WriteLine(messages.ReportNoMatches);
                return CliExitCodes.Ok;
            }
            AppConfig config = ConfigFile.Load(paths.ConfigPath(), paths.Home());
            IReadOnlyList<string> lines = Runs.Report(connection, config, paths, last, time, messages);
            foreach (string line in lines.Count > 0 ? lines : [messages.ReportNoMatches]) {
                output.WriteLine(line);
            }
            return CliExitCodes.Ok;
        } catch (Exception error) when (ReadFailures.Of(error, paths, messages) is ReadFailure failure) {
            LogEvents.ReportFailed(logger, failure.Code);
            foreach (string line in failure.Lines) {
                output.WriteLine(line);
            }
            return CliExitCodes.Failure;
        }
    }
}
