using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class UnpairCommand : Command {
    public const string CommandName = "unpair";

    private const string keysFailure = "keys";

    public UnpairCommand(CliServices services)
        : this(services, Environment.GetEnvironmentVariables(), Console.IsInputRedirected, Link.OpenAsync) {
    }

    public UnpairCommand(
        CliServices services, IDictionary environment, bool inputRedirected, PairCommand.LinkOpener openLink)
        : base(CommandName, services.Messages.UnpairDescription) {
        SetAction((_, cancellationToken) => DeviceCommandRun.RunAsync(
            services,
            environment,
            !inputRedirected,
            openLink,
            (link, output, logger, token) =>
                UnpairAsync(link, output, logger, services.Messages, new Paths(environment), token),
            cancellationToken));
    }

    private static async Task<int> UnpairAsync(
        Link link,
        TextWriter output,
        ILogger logger,
        IMessages messages,
        Paths paths,
        CancellationToken cancellationToken) {
        using FileLock? held = FileLock.TryAcquire(paths.PairLockPath(), logger);
        if (held is null) {
            await output.WriteLineAsync(messages.PairAlreadyRunning).ConfigureAwait(false);
            return CliExitCodes.Failure;
        }
        int removed = link.RemoveAllDevices();
        try {
            using LinkKeyPair rotated = await link.RotateKeysAsync(cancellationToken).ConfigureAwait(false);
        } catch (LinkKeyException) {
            LogEvents.DevicesFailed(logger, keysFailure);
            await output.WriteLineAsync(messages.UnpairKeysFailed).ConfigureAwait(false);
            return CliExitCodes.Failure;
        }
        if (removed == 0) {
            await output.WriteLineAsync(messages.UnpairNone).ConfigureAwait(false);
            return CliExitCodes.Ok;
        }
        LogEvents.DevicesUnpaired(logger, removed);
        await output.WriteLineAsync(messages.Unpaired(removed)).ConfigureAwait(false);
        return CliExitCodes.Ok;
    }
}
