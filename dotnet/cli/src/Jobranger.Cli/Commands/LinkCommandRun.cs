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
internal static class LinkCommandRun {
    private const string configFailure = "config_invalid";
    private const string schemaFailure = "schema_too_new";
    private const string databaseFailure = "database_unreadable";
    private const string dirFailure = "dir_not_writable";
    private const string secretStoreFailure = "secret_store_failed";

    public static async Task<int> RunAsync(
        CliServices services,
        IDictionary environment,
        bool canPrompt,
        LinkOpener openLink,
        Action<ILogger, string> logFailure,
        LinkAction action,
        CancellationToken cancellationToken) {
        ILogger logger = services.Logs.CreateLogger(typeof(LinkCommandRun).FullName!);
        TextWriter output = services.Console.Profile.Out.Writer;
        IMessages messages = services.Messages;
        Paths paths = new(environment);
        try {
            ServiceUrls urls = ServiceUrls.From(environment);
            await using Link link =
                await openLink(environment, paths, urls, logger, canPrompt, cancellationToken).ConfigureAwait(false);
            return await action(new OpenedLink(link, urls, paths, output, logger), cancellationToken)
                .ConfigureAwait(false);
        } catch (ConfigInvalidException invalid) {
            return await FailAsync(configFailure, [.. invalid.Errors.Select(error => error.Describe(messages))])
                .ConfigureAwait(false);
        } catch (SchemaTooNewException) {
            return await FailAsync(schemaFailure, [messages.DatabaseTooNew]).ConfigureAwait(false);
        } catch (SecretStoreUnavailableException) {
            return await FailAsync(secretStoreFailure, [messages.SecretStoreFailed]).ConfigureAwait(false);
        } catch (PrivateDirException dir) {
            return await FailAsync(dirFailure, [messages.DirNotWritable(dir.Path)]).ConfigureAwait(false);
        } catch (Exception error) when (error is SqliteException or PrivateFileException) {
            return await FailAsync(databaseFailure, [messages.DatabaseUnreadable(paths.DbPath())]).ConfigureAwait(false);
        }

        async Task<int> FailAsync(string failure, string[] lines) {
            logFailure(logger, failure);
            foreach (string line in lines) {
                await output.WriteLineAsync(line).ConfigureAwait(false);
            }
            return CliExitCodes.Failure;
        }
    }

    public sealed record OpenedLink(Link Link, ServiceUrls Urls, Paths Paths, TextWriter Output, ILogger Logger);

    public delegate Task<int> LinkAction(OpenedLink opened, CancellationToken cancellationToken);

    public delegate Task<Link> LinkOpener(
        IDictionary environment,
        Paths paths,
        ServiceUrls urls,
        ILogger logger,
        bool canPrompt,
        CancellationToken cancellationToken);
}
