using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Features.Pairing;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class PairCommand : Command {
    public const string CommandName = "pair";

    private const string configFailure = "config_invalid";
    private const string schemaFailure = "schema_too_new";
    private const string databaseFailure = "database_unreadable";

    public PairCommand(CliServices services)
        : this(services, Environment.GetEnvironmentVariables(), TimeProvider.System) {
    }

    public PairCommand(CliServices services, IDictionary environment, TimeProvider time)
        : base(CommandName, services.Messages.PairDescription) {
        SetAction((_, cancellationToken) => RunAsync(services, environment, time, cancellationToken));
    }

    private static async Task<int> RunAsync(
        CliServices services, IDictionary environment, TimeProvider time, CancellationToken cancellationToken) {
        ILogger logger = services.Logs.CreateLogger(typeof(PairCommand).FullName!);
        TextWriter output = services.Console.Profile.Out.Writer;
        IMessages messages = services.Messages;
        Paths paths = new(environment);
        try {
            ServiceUrls urls = ServiceUrls.From(environment);
            bool canPrompt = services.Console.Profile.Capabilities.Interactive;
            await using Link link =
                await Link.OpenAsync(environment, paths, urls, logger, canPrompt, cancellationToken).ConfigureAwait(false);
            Pairing pairing = new(link, messages, urls, paths, Environment.MachineName, time, logger);
            return await pairing.RunAsync(services.Console, prompt => Ask(output, prompt), cancellationToken)
                .ConfigureAwait(false);
        } catch (ConfigInvalidException invalid) {
            return await FailAsync(
                output, logger, configFailure, [.. invalid.Errors.Select(error => error.Describe(messages))])
                .ConfigureAwait(false);
        } catch (SchemaTooNewException) {
            return await FailAsync(output, logger, schemaFailure, [messages.DatabaseTooNew]).ConfigureAwait(false);
        } catch (SqliteException) {
            return await FailAsync(output, logger, databaseFailure, [messages.DatabaseUnreadable(paths.DbPath())])
                .ConfigureAwait(false);
        }
    }

    private static async Task<int> FailAsync(TextWriter output, ILogger logger, string failure, string[] lines) {
        LogEvents.PairingFailed(logger, failure);
        foreach (string line in lines) {
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }
        return CliExitCodes.Failure;
    }

    private static string Ask(TextWriter output, string prompt) {
        output.Write(prompt);
        output.Flush();
        return Console.In.ReadLine() ?? string.Empty;
    }
}
