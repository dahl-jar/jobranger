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
    private const string lockFailure = "lock_unreadable";

    public UnpairCommand(CliServices services)
        : this(services, Environment.GetEnvironmentVariables(), Console.IsInputRedirected, Link.OpenAsync) {
    }

    public UnpairCommand(
        CliServices services, IDictionary environment, bool inputRedirected, LinkCommandRun.LinkOpener openLink)
        : base(CommandName, services.Messages.UnpairDescription) {
        SetAction((_, cancellationToken) => LinkCommandRun.RunAsync(
            services,
            environment,
            !inputRedirected,
            openLink,
            LogEvents.DevicesFailed,
            (opened, token) => UnpairAsync(opened, services.Messages, token),
            cancellationToken));
    }

    private static async Task<int> UnpairAsync(
        LinkCommandRun.OpenedLink opened, IMessages messages, CancellationToken cancellationToken) {
        FileLock? held;
        try {
            held = FileLock.TryAcquire(opened.Paths.PairLockPath(), opened.Logger);
        } catch (PrivateFileException file) {
            return await FailAsync(opened, lockFailure, messages.FileUnreadable(file.Path)).ConfigureAwait(false);
        }
        if (held is null) {
            await opened.Output.WriteLineAsync(messages.PairAlreadyRunning).ConfigureAwait(false);
            return CliExitCodes.Failure;
        }
        using (held) {
            return await RemoveAndRotateAsync(opened, messages, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<int> RemoveAndRotateAsync(
        LinkCommandRun.OpenedLink opened, IMessages messages, CancellationToken cancellationToken) {
        int removed = opened.Link.RemoveAllDevices();
        if (removed > 0) {
            LogEvents.DevicesUnpaired(opened.Logger, removed);
        }
        try {
            using LinkKeyPair rotated = await opened.Link.RotateKeysAsync(cancellationToken).ConfigureAwait(false);
        } catch (LinkKeyException error) when (error.InnerException is PrivateFileException file) {
            return await FailAsync(opened, keysFailure, messages.UnpairKeysFileFailed(file.Path)).ConfigureAwait(false);
        } catch (LinkKeyException) {
            return await FailAsync(opened, keysFailure, messages.UnpairKeysFailed).ConfigureAwait(false);
        }
        string line = removed == 0 ? messages.UnpairNone : messages.Unpaired(removed);
        await opened.Output.WriteLineAsync(line).ConfigureAwait(false);
        return CliExitCodes.Ok;
    }

    private static async Task<int> FailAsync(LinkCommandRun.OpenedLink opened, string failure, string line) {
        LogEvents.DevicesFailed(opened.Logger, failure);
        await opened.Output.WriteLineAsync(line).ConfigureAwait(false);
        return CliExitCodes.Failure;
    }
}
