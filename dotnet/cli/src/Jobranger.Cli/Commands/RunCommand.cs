using System.Collections;
using System.CommandLine;
using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Features.Providers;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Storage;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using DeliveryEntry = Jobranger.Cli.Features.Delivery.Delivery;
using DigestEntry = Jobranger.Cli.Features.Digest.Digest;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class RunCommand : Command {
    public const string CommandName = "run";

    private const string sourceSeparator = ", ";
    private const string secretStoreFailed = "secret store failed";
    private const string configMissing = "config_missing";
    private const string configFailure = "config_invalid";
    private const string schemaFailure = "schema_too_new";
    private const string databaseFailure = "database_unreadable";
    private const string fileFailure = "file_unreadable";

    private static readonly TimeSpan cancelLimit = TimeSpan.FromMilliseconds(1500);

    public RunCommand(CliServices services)
        : this(
            services,
            Environment.GetEnvironmentVariables(),
            TimeProvider.System,
            Console.IsInputRedirected,
            RunSeams.Production) {
    }

    public RunCommand(CliServices services, IDictionary environment, TimeProvider time, bool inputRedirected, RunSeams seams)
        : base(CommandName, services.Messages.RunDescription) {
        SetAction((_, cancellationToken) =>
            RunAsync(new RunContext(services, environment, time, !inputRedirected, seams), cancellationToken));
    }

    public static string CountsLine(RunResult result, IMessages messages) =>
        messages.RunCounts(result.AdsRead, result.NewAds, result.ReportedIds.Count);

    private static string? OutcomeLine(DeliveryResult delivered, bool website, IMessages messages) => delivered switch {
        { EmailError: string reason } => messages.RunEmailFailed(reason),
        { EmailTo: string email } when website => messages.RunSentBoth(email),
        { EmailTo: string email } => messages.RunSentEmail(email),
        _ when website => messages.RunSavedForWebsite,
        _ => null,
    };

    private static async Task<int> RunAsync(RunContext context, CancellationToken cancellationToken) {
        ILogger logger = context.Services.Logs.CreateLogger(typeof(RunCommand).FullName!);
        using PosixSignals signals = new(cancellationToken);
        Task<int> work = Task.Run(() => PassAsync(context, logger, signals.Token), CancellationToken.None);
        Task first = await Task.WhenAny(work, ForcedStopAsync(context.Time, signals.Token)).ConfigureAwait(false);
        if (first == work) {
            return await work.ConfigureAwait(false);
        }
        LogEvents.RunStopForced(logger);
        return CliExitCodes.Cancelled;
    }

    private static async Task ForcedStopAsync(TimeProvider time, CancellationToken cancellationToken) {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        await Task.Delay(cancelLimit, time, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<int> PassAsync(RunContext context, ILogger logger, CancellationToken cancellationToken) {
        TextWriter output = context.Output;
        IMessages messages = context.Messages;
        Paths paths = new(context.Environment);
        try {
            return await StagesAsync(context, paths, logger, cancellationToken).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return CliExitCodes.Cancelled;
        } catch (ConfigMissingException) {
            return await FailAsync(output, logger, configMissing, [messages.ConfigMissing]).ConfigureAwait(false);
        } catch (ConfigInvalidException invalid) {
            return await FailAsync(output, logger, configFailure, invalid.Errors.Select(error => error.Describe(messages)))
                .ConfigureAwait(false);
        } catch (SchemaTooNewException) {
            return await FailAsync(output, logger, schemaFailure, [messages.DatabaseTooNew]).ConfigureAwait(false);
        } catch (SqliteException) {
            return await FailAsync(output, logger, databaseFailure, [messages.DatabaseUnreadable(paths.DbPath())])
                .ConfigureAwait(false);
        } catch (PrivateFileException error) {
            return await FailAsync(output, logger, fileFailure, [messages.FileUnreadable(error.Path)]).ConfigureAwait(false);
        }
    }

    private static async Task<int> StagesAsync(RunContext context, Paths paths, ILogger logger, CancellationToken cancellationToken) {
        AppConfig config = ConfigFile.Load(paths.ConfigPath(), paths.Home());
        ServiceUrls urls = ServiceUrls.From(context.Environment);
        ISecretStore? store = await OpenSecretsAsync(context, logger, cancellationToken).ConfigureAwait(false);
        Credentials credentials = new(context.Environment, store, new FileSecretStore(paths.SecretsPath(), logger));
        using SqliteConnection database = Database.Open(paths.DbPath(), logger);
        using HttpClient http = context.Seams.CreateSourceClient(context.Time);
        RunsEntry runs = new(
            database,
            paths,
            http,
            (model, supportsEffort, token) => OpenModelAsync(context, credentials, logger, model, supportsEffort, token),
            context.Seams.EnabledSources,
            context.Time,
            logger);
        string sources = SourceList(context.Seams.EnabledSources(config.Sources).Select(source => source.Name), context.Messages);
        RunRequest request = new(config, new ReadingLine(context.Output, context.Messages, sources));
        RunResult result = await runs.RunAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.Status == RunStatus.Locked) {
            await context.Output.WriteLineAsync(context.Messages.RunLocked).ConfigureAwait(false);
            return CliExitCodes.Ok;
        }
        if (result.Status == RunStatus.Cancelled) {
            return CliExitCodes.Cancelled;
        }
        cancellationToken.ThrowIfCancellationRequested();
        await WriteSummaryAsync(context.Output, result, context.Messages).ConfigureAwait(false);
        using IDisposable scope = RunScope.Bind(result.RunId.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));
        DeliveryEntry delivery = new(database, credentials, urls, context.Messages, context.Seams.Transport, context.Time, logger);
        DeliveryResult delivered = await delivery.DeliverAsync(config, result, cancellationToken).ConfigureAwait(false);
        if (OutcomeLine(delivered, config.Notify.Website, context.Messages) is string line) {
            await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }
        return delivered.EmailError is not null || AllFailed(result) ? CliExitCodes.Failure : CliExitCodes.Ok;
    }

    private static async Task WriteSummaryAsync(TextWriter output, RunResult result, IMessages messages) {
        await output.WriteLineAsync(CountsLine(result, messages)).ConfigureAwait(false);
        foreach (string line in DeliveryEntry.SummaryLines(result, messages)) {
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }
        if (AllFailed(result)) {
            await output.WriteLineAsync(messages.DigestCouldNotRead(SourceList(result.FailedSources, messages))).ConfigureAwait(false);
        }
    }

    private static bool AllFailed(RunResult result) => result.FailedSources.Count > 0 && result.SourcesRead.Count == 0;

    private static string SourceList(IEnumerable<string> sources, IMessages messages) =>
        string.Join(sourceSeparator, sources.Select(source => DigestEntry.SourcePhrase(source, messages)));

    private static async Task<ISecretStore?> OpenSecretsAsync(RunContext context, ILogger logger, CancellationToken cancellationToken) {
        try {
            return await context.Seams.OpenSecrets(context.Environment, context.CanPrompt, cancellationToken).ConfigureAwait(false);
        } catch (SecretStoreUnavailableException error) {
            LogEvents.RunSecretStoreFailed(logger, error);
            return null;
        }
    }

    private static async Task<IModelClient> OpenModelAsync(
        RunContext context,
        Credentials credentials,
        ILogger logger,
        ModelConfig model,
        bool supportsEffort,
        CancellationToken cancellationToken) {
        try {
            return await context.Seams.OpenModel(model, credentials, supportsEffort, context.Time, cancellationToken)
                .ConfigureAwait(false);
        } catch (SecretStoreUnavailableException error) {
            LogEvents.RunSecretStoreFailed(logger, error);
            throw new ModelSetupException(secretStoreFailed);
        }
    }

    private static async Task<int> FailAsync(TextWriter output, ILogger logger, string failure, IEnumerable<string> lines) {
        LogEvents.RunFailed(logger, failure);
        foreach (string line in lines) {
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }
        return CliExitCodes.Failure;
    }

    private sealed record RunContext(CliServices Services, IDictionary Environment, TimeProvider Time, bool CanPrompt, RunSeams Seams) {
        public TextWriter Output => Services.Console.Profile.Out.Writer;

        public IMessages Messages => Services.Messages;
    }

    private sealed class ReadingLine(TextWriter output, IMessages messages, string sources) : IRunEvents {
        private bool written;

        public void Emit(RunEvent runEvent) {
            if (runEvent.Stage == RunStage.SourceStarted && !written) {
                written = true;
                output.WriteLine(messages.RunReading(sources));
            }
        }
    }
}
