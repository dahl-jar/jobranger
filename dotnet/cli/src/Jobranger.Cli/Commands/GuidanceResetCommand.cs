using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Rechecks;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using RechecksEntry = Jobranger.Cli.Features.Rechecks.Rechecks;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class GuidanceResetCommand : Command {
    public const string CommandName = "reset";

    public GuidanceResetCommand(CliServices services, EditServices editServices)
        : base(CommandName, services.Messages.GuidanceResetDescription) {
        SetAction(_ => RunAsync(services, editServices));
    }

    private static async Task<int> RunAsync(CliServices services, EditServices editServices) {
        ILogger logger = services.Logs.CreateLogger(typeof(GuidanceResetCommand).FullName!);
        IMessages messages = services.Messages;
        TextWriter output = services.Console.Profile.Out.Writer;
        Paths paths = new(editServices.Environment);
        SaveResult result;
        try {
            result = new RechecksEntry(paths, editServices.Time, logger).ResetGuidance();
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            return await EditSession.FailSaveAsync(output, messages, logger, paths.GuidancePath()).ConfigureAwait(false);
        }
        await EditSession.WriteSavedAsync(output, messages, messages.GuidanceBackToDefault, result.Estimate)
            .ConfigureAwait(false);
        return CliExitCodes.Ok;
    }
}
