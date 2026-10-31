using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal static class DeviceCommandRun {
    private const string configFailure = "config_invalid";
    private const string schemaFailure = "schema_too_new";
    private const string databaseFailure = "database_unreadable";
    private const string secretStoreFailure = "secret_store_failed";

    public static async Task<int> RunAsync(
        CliServices services,
        IDictionary environment,
        bool canPrompt,
        PairCommand.LinkOpener openLink,
        DeviceAction action,
        CancellationToken cancellationToken) {
        ILogger logger = services.Logs.CreateLogger(typeof(DeviceCommandRun).FullName!);
        TextWriter output = services.Console.Profile.Out.Writer;
        IMessages messages = services.Messages;
        Paths paths = new(environment);
        try {
            ServiceUrls urls = ServiceUrls.From(environment);
            await using Link link =
                await openLink(environment, paths, urls, logger, canPrompt, cancellationToken).ConfigureAwait(false);
            return await action(link, output, logger, cancellationToken).ConfigureAwait(false);
        } catch (ConfigInvalidException invalid) {
            return await FailAsync(
                output, logger, configFailure, [.. invalid.Errors.Select(error => error.Describe(messages))])
                .ConfigureAwait(false);
        } catch (SchemaTooNewException) {
            return await FailAsync(output, logger, schemaFailure, [messages.DatabaseTooNew]).ConfigureAwait(false);
        } catch (SecretStoreUnavailableException) {
            return await FailAsync(output, logger, secretStoreFailure, [messages.DevicesSecretStoreFailed])
                .ConfigureAwait(false);
        } catch (Exception error) when (error is SqliteException or PrivateFileException) {
            return await FailAsync(output, logger, databaseFailure, [messages.DatabaseUnreadable(paths.DbPath())])
                .ConfigureAwait(false);
        }
    }

    private static async Task<int> FailAsync(TextWriter output, ILogger logger, string failure, string[] lines) {
        LogEvents.DevicesFailed(logger, failure);
        foreach (string line in lines) {
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }
        return CliExitCodes.Failure;
    }

    public delegate Task<int> DeviceAction(
        Link link, TextWriter output, ILogger logger, CancellationToken cancellationToken);
}
