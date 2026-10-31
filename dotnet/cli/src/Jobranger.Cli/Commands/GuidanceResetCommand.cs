using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Rechecks;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using RechecksEntry = Jobranger.Cli.Features.Rechecks.Rechecks;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class GuidanceResetCommand : Command {
    public const string CommandName = "reset";

    public GuidanceResetCommand(CliServices services, EditSeams seams)
        : base(CommandName, services.Messages.GuidanceResetDescription) {
        SetAction(_ => RunAsync(services, seams));
    }

    private static async Task<int> RunAsync(CliServices services, EditSeams seams) {
        ILogger logger = services.Logs.CreateLogger(typeof(GuidanceResetCommand).FullName!);
        IMessages messages = services.Messages;
        TextWriter output = services.Console.Profile.Out.Writer;
        Paths paths = new(seams.Environment);
        SaveResult result;
        try {
            result = new RechecksEntry(paths, seams.Time, logger).ResetGuidance();
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            LogEvents.EditFailed(logger, EditSession.SaveFailure);
            await output.WriteLineAsync(messages.SaveFailed(paths.GuidancePath())).ConfigureAwait(false);
            return CliExitCodes.Failure;
        }
        await EditSession.WriteSavedAsync(output, messages, messages.GuidanceBackToDefault, result.Estimate)
            .ConfigureAwait(false);
        return CliExitCodes.Ok;
    }
}
