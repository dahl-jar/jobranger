using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class DevicesRevokeCommand : Command {
    public const string CommandName = "revoke";

    private const string idArgument = "id";

    public DevicesRevokeCommand(CliServices services, IDictionary environment, LinkCommandRun.LinkOpener openLink)
        : base(CommandName, services.Messages.DevicesRevokeDescription) {
        Argument<string> id = new(idArgument) { Description = services.Messages.DevicesRevokeIdDescription };
        Arguments.Add(id);
        SetAction((parseResult, cancellationToken) => LinkCommandRun.RunAsync(
            services,
            environment,
            canPrompt: false,
            openLink,
            LogEvents.DevicesFailed,
            (opened, _) => RevokeAsync(
                opened.Link, opened.Output, opened.Logger, services.Messages, parseResult.GetRequiredValue(id)),
            cancellationToken));
    }

    private static async Task<int> RevokeAsync(
        Link link, TextWriter output, ILogger logger, IMessages messages, string id) {
        IReadOnlyList<Device> matches = link.DevicesWithIdPrefix(id);
        if (matches.Count == 0) {
            await output.WriteLineAsync(messages.DevicesNoMatch(id)).ConfigureAwait(false);
            return CliExitCodes.Failure;
        }
        if (matches.Count > 1) {
            await output.WriteLineAsync(messages.DevicesAmbiguous(id)).ConfigureAwait(false);
            return CliExitCodes.Failure;
        }
        Device match = matches[0];
        link.RemoveDevice(match.Id);
        LogEvents.DevicesRevoked(logger);
        await output.WriteLineAsync(messages.DevicesRevoked(match.Name)).ConfigureAwait(false);
        return CliExitCodes.Ok;
    }
}
