using System.Collections;
using System.CommandLine;
using System.Globalization;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Setup;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using DeliveryEntry = Jobranger.Cli.Features.Delivery.Delivery;
using SetupEntry = Jobranger.Cli.Features.Setup.Setup;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class InitCommand : Command {
    public const string CommandName = "init";

    private const string notTerminal = "not_a_terminal";
    private const string configFailure = "config_invalid";
    private const string saveFailure = "save_failed";
    private const string storeFailure = "secret_store_failed";
    private const string stampFormat = "yyyy-MM-dd HH:mm:ss";

    public InitCommand(CliServices services)
        : this(
            services,
            Environment.GetEnvironmentVariables(),
            TimeProvider.System,
            Console.IsInputRedirected,
            InitSeams.Production) {
    }

    public InitCommand(CliServices services, IDictionary environment, TimeProvider time, bool inputRedirected, InitSeams seams)
        : base(CommandName, services.Messages.InitDescription) {
        SetAction((_, cancellationToken) =>
            RunAsync(new InitContext(services, environment, time, inputRedirected, seams), cancellationToken));
    }

    private static async Task<int> RunAsync(InitContext context, CancellationToken cancellationToken) {
        ILogger logger = context.Services.Logs.CreateLogger(typeof(InitCommand).FullName!);
        IPrompter? prompter = context.InputRedirected
            ? null
            : context.Seams.CreatePrompter(context.Services.Console, context.Messages);
        if (prompter is null) {
            LogEvents.InitFailed(logger, notTerminal);
            await context.Errors.WriteLineAsync(context.Messages.InitNeedsTerminal).ConfigureAwait(false);
            return CliExitCodes.Failure;
        }
        using PosixSignals signals = new(cancellationToken);
        SetupEntry setup = SetupEntry.Init(new SetupContext(
            prompter,
            context.Output,
            context.Messages,
            new Paths(context.Environment),
            context.Environment,
            context.Time,
            new SetupSeams(
                context.Seams.CreateModelHandler,
                context.Seams.OpenSecrets,
                (email, secret, token) => SendTestEmailAsync(context, email, secret, token)),
            logger));
        return await RunSetupAsync(setup, context, logger, signals.Token).ConfigureAwait(false);
    }

    private static async Task<int> RunSetupAsync(
        SetupEntry setup, InitContext context, ILogger logger, CancellationToken cancellationToken) {
        IMessages messages = context.Messages;
        try {
            await setup.RunInitAsync(cancellationToken).ConfigureAwait(false);
            return CliExitCodes.Ok;
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            return await CancelledAsync(context).ConfigureAwait(false);
        } catch (SetupCancelledException) {
            return await CancelledAsync(context).ConfigureAwait(false);
        } catch (ConfigInvalidException invalid) {
            return await FailAsync(context, logger, configFailure, invalid.Errors.Select(error => error.Describe(messages)))
                .ConfigureAwait(false);
        } catch (SecretStoreUnavailableException) {
            return await FailAsync(context, logger, storeFailure, [messages.InitSecretStoreFailed]).ConfigureAwait(false);
        } catch (SetupSaveException failure) {
            return await FailAsync(context, logger, FailureCode(failure), SaveLines(failure, context)).ConfigureAwait(false);
        }
    }

    private static string FailureCode(SetupSaveException failure) =>
        failure.InnerException is SecretStoreUnavailableException ? storeFailure : saveFailure;

    private static List<string> SaveLines(SetupSaveException failure, InitContext context) {
        List<string> lines = [FailureLine(failure.InnerException, context)];
        if (failure.SecretsWritten > 0) {
            lines.Add(context.Messages.InitSecretsKept);
        }
        return lines;
    }

    private static string FailureLine(Exception? cause, InitContext context) => cause switch {
        SecretStoreUnavailableException => context.Messages.InitSecretStoreFailed,
        PrivateDirException { IsFile: true } dir => context.Messages.InitConfigDirIsFile(dir.Path),
        PrivateFileException { IsUnreadable: true } file => context.Messages.InitSecretsUnreadable(file.Path),
        TimeoutException => context.Messages.InitSecretsBusy(new Paths(context.Environment).SecretsPath()),
        _ => context.Messages.InitSaveFailed(FailedPath(cause, new Paths(context.Environment))),
    };

    private static string FailedPath(Exception? cause, Paths paths) => cause switch {
        PrivateDirException dir => dir.Path,
        PrivateFileException file => file.Path,
        _ => paths.ConfigPath(),
    };

    private static async Task<string?> SendTestEmailAsync(
        InitContext context, EmailConfig email, string? secret, CancellationToken cancellationToken) {
        IMessages messages = context.Messages;
        string body = messages.InitTestBody(context.Time.GetLocalNow().ToString(stampFormat, CultureInfo.InvariantCulture));
        RenderedDigest rendered = new(messages.InitTestSubject, $"<p>{body}</p>", body, []);
        SendFailure? failure = await DeliveryEntry.SendTestAsync(
            email, secret, rendered, context.Seams.Transport, messages, context.Time, cancellationToken).ConfigureAwait(false);
        return failure?.Reason;
    }

    private static async Task<int> CancelledAsync(InitContext context) {
        await context.Output.WriteLineAsync(context.Messages.InitCancelled).ConfigureAwait(false);
        return CliExitCodes.Cancelled;
    }

    private static async Task<int> FailAsync(InitContext context, ILogger logger, string failure, IEnumerable<string> lines) {
        LogEvents.InitFailed(logger, failure);
        foreach (string line in lines) {
            await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }
        return CliExitCodes.Failure;
    }

    private sealed record InitContext(
        CliServices Services, IDictionary Environment, TimeProvider Time, bool InputRedirected, InitSeams Seams) {
        public TextWriter Output => Services.Console.Profile.Out.Writer;

        public TextWriter Errors => Services.ErrorConsole.Profile.Out.Writer;

        public IMessages Messages => Services.Messages;
    }
}
