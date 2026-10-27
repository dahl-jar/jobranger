using System.Collections;
using System.Collections.Frozen;
using System.Runtime.Versioning;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Text;
using Spectre.Console;

namespace Jobranger.Cli.Features.Updates;

[UnsupportedOSPlatform("windows")]
internal sealed class Updates(
    IAnsiConsole errorConsole,
    IMessages messages,
    IDictionary environment,
    string cachePath,
    string? processPath,
    string version,
    TimeProvider time,
    Func<HttpMessageHandler> createHandler) {
    private const string optOutVariable = "JOBRANGER_NO_UPDATE_CHECK";

    private static readonly char[] suffixStarts = ['-', '+'];
    private static readonly FrozenSet<string> offValues = FrozenSet.Create(StringComparer.Ordinal, "", "0", "false", "no", "off");

    public async Task MaybeNotifyAsync(bool scheduled, CancellationToken cancellationToken) {
        if (scheduled || !errorConsole.Profile.Capabilities.Interactive || IsOptedOut()) {
            return;
        }
        using HttpClient client = new(createHandler());
        Version? latest = await VersionCheck.LatestAsync(client, cachePath, time.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        string release = version.Split(suffixStarts)[0];
        if (latest is null || !Version.TryParse(release, out Version? current) || latest <= current) {
            return;
        }
        InstallMethod method = processPath is null ? InstallMethod.Archive : InstallMethods.Detect(processPath, environment);
        string notice = messages.UpdateAvailable(latest.ToString(), current.ToString(), InstallMethods.UpgradeCommand(method));
        await errorConsole.Profile.Out.Writer.WriteLineAsync(notice.AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private bool IsOptedOut() =>
        EnvironmentVariables.Read(environment, optOutVariable) is string value
        && !offValues.Contains(value.Trim().ToLowerInvariant());
}
