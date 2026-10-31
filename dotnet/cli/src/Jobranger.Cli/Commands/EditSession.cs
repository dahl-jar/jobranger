using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Rechecks;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using RechecksEntry = Jobranger.Cli.Features.Rechecks.Rechecks;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class EditSession(CliServices services, EditServices editServices, Paths paths, ILogger logger) {
    private const string saveFailure = "save_failed";
    private const string notStartedFailure = "editor_not_started";
    private const string failedExitFailure = "editor_failed_exit";
    private const string tempFileFailure = "edit_file_failed";
    private const char finalLineEnd = '\n';

    private readonly IMessages messages = services.Messages;
    private readonly TextWriter output = services.Console.Profile.Out.Writer;

    public static async Task WriteSavedAsync(
        TextWriter output, IMessages messages, string savedLine, RecheckEstimate? estimate) {
        await output.WriteLineAsync(savedLine).ConfigureAwait(false);
        if (estimate is { Ads: > 0 } outdated) {
            string line = outdated.CostUsd is decimal cost
                ? messages.RecheckAdsCost(outdated.Ads, cost)
                : messages.RecheckAds(outdated.Ads);
            await output.WriteLineAsync(line).ConfigureAwait(false);
        }
    }

    public static async Task<int> FailSaveAsync(TextWriter output, IMessages messages, ILogger logger, string path) {
        LogEvents.EditFailed(logger, saveFailure);
        await output.WriteLineAsync(messages.SaveFailed(path)).ConfigureAwait(false);
        return CliExitCodes.Failure;
    }

    public static Task<int> RunAsync(
        CliServices services,
        EditServices editServices,
        Type command,
        Func<Paths, RechecksEntry, EditRequest> request,
        CancellationToken cancellationToken) {
        ILogger logger = services.Logs.CreateLogger(command.FullName!);
        Paths paths = new(editServices.Environment);
        RechecksEntry rechecks = new(paths, editServices.Time, logger);
        return new EditSession(services, editServices, paths, logger).RunAsync(request(paths, rechecks), cancellationToken);
    }

    private async Task<int> RunAsync(EditRequest request, CancellationToken cancellationToken) {
        string? text = null;
        int exit = ReadFailures.PrintLines(
            () => {
                text = request.Load();
                return [];
            },
            failure => LogEvents.EditFailed(logger, failure),
            paths,
            messages,
            output);
        return text is string loadedText
            ? await EditAsync(request, loadedText, cancellationToken).ConfigureAwait(false)
            : exit;
    }

    private async Task<int> EditAsync(EditRequest request, string text, CancellationToken cancellationToken) {
        EditorRunner editor = new(editServices.Environment, paths.StateDir(), editServices.Process, logger);
        string current = text;
        while (true) {
            string edited;
            try {
                edited = await editor.EditAsync(current, request.Suffix, cancellationToken).ConfigureAwait(false);
            } catch (EditorException failed) {
                if (failed.EditedText is not string changed
                    || IsUnchanged(changed, current)
                    || !await WantsSaveAfterFailureAsync().ConfigureAwait(false)) {
                    return await FailAsync(failed).ConfigureAwait(false);
                }
                edited = changed;
            }
            if (IsUnchanged(edited, current)) {
                await output.WriteLineAsync(messages.NoChanges).ConfigureAwait(false);
                return CliExitCodes.Ok;
            }
            SaveResult saved;
            try {
                saved = request.Save(edited);
            } catch (ConfigInvalidException invalid) {
                if (!await WantsAnotherEditAsync(invalid).ConfigureAwait(false)) {
                    await output.WriteLineAsync(request.KeptLine).ConfigureAwait(false);
                    return CliExitCodes.Ok;
                }
                current = edited;
                continue;
            } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
                return await FailSaveAsync(output, messages, logger, request.TargetPath).ConfigureAwait(false);
            }
            await WriteSavedAsync(output, messages, request.SavedLine(saved), saved.Estimate).ConfigureAwait(false);
            return CliExitCodes.Ok;
        }
    }

    private async Task<bool> WantsAnotherEditAsync(ConfigInvalidException invalid) {
        foreach (ConfigError error in invalid.Errors) {
            await output.WriteLineAsync(error.Describe(messages)).ConfigureAwait(false);
        }
        return await AskAsync(messages.EditAgain).ConfigureAwait(false) is string answer
            && !messages.EditNoAnswers.Contains(answer.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<bool> WantsSaveAfterFailureAsync() =>
        await AskAsync(messages.EditorFailedSave).ConfigureAwait(false) is string answer
            && messages.EditYesAnswers.Contains(answer.Trim(), StringComparer.OrdinalIgnoreCase);

    private async Task<string?> AskAsync(string question) {
        await output.WriteAsync(question).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return editServices.ReadLine();
    }

    private static bool IsUnchanged(string edited, string opened) =>
        edited == opened || edited == opened + finalLineEnd;

    private async Task<int> FailAsync(EditorException failed) {
        (string failure, string line) = failed.Problem switch {
            EditorProblem.NotStarted => (notStartedFailure, messages.EditorNotStarted(failed.Program)),
            EditorProblem.FailedExit => (failedExitFailure, messages.EditorFailedExit),
            _ => (tempFileFailure, messages.EditorTempFile),
        };
        LogEvents.EditFailed(logger, failure);
        await output.WriteLineAsync(line).ConfigureAwait(false);
        return CliExitCodes.Failure;
    }
}
