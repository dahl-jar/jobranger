using System.Collections;
using System.CommandLine;
using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class DevicesListCommand : Command {
    public const string CommandName = "list";

    private const int shortIdChars = 8;

    public DevicesListCommand(
        CliServices services, IDictionary environment, TimeProvider time, LinkCommandRun.LinkOpener openLink)
        : base(CommandName, services.Messages.DevicesListDescription) {
        SetAction((_, cancellationToken) => LinkCommandRun.RunAsync(
            services,
            environment,
            canPrompt: false,
            openLink,
            LogEvents.DevicesFailed,
            (opened, _) => ListAsync(opened.Link, opened.Output, services.Messages, time),
            cancellationToken));
    }

    private static async Task<int> ListAsync(Link link, TextWriter output, IMessages messages, TimeProvider time) {
        IReadOnlyList<Device> devices = link.Devices();
        if (devices.Count == 0) {
            await output.WriteLineAsync(messages.DevicesNone).ConfigureAwait(false);
            return CliExitCodes.Ok;
        }
        foreach (Device device in devices) {
            string line = messages.DeviceLine(
                string.Concat(device.Id.Take(shortIdChars)),
                device.Name,
                Day(device.PairedAt, time),
                device.LastSeen is string seen ? Day(seen, time) : messages.DeviceNeverSeen);
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }
        return CliExitCodes.Ok;
    }

    private static string Day(string stored, TimeProvider time) =>
        StoredTime.Format(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(
            DateTimeOffset.Parse(stored, CultureInfo.InvariantCulture), time.LocalTimeZone).DateTime));
}
