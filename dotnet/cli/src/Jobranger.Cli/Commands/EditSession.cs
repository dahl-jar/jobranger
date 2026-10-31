using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Budget;
using Jobranger.Cli.Features.Rechecks;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class EditSession(CliServices services, EditSeams seams, ILogger logger) {
    public const string SaveFailure = "save_failed";

    private const string notStartedFailure = "editor_not_started";
    private const string failedExitFailure = "editor_failed_exit";
    private const string tempFileFailure = "edit_file_failed";

    private readonly IMessages messages = services.Messages;
    private readonly TextWriter output = services.Console.Profile.Out.Writer;
    private readonly Paths paths = new(seams.Environment);

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

    public async Task<int> RunAsync(EditRequest request, CancellationToken cancellationToken) {
        string? text = null;
        int loaded = ReadFailures.PrintLines(
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
            : loaded;
    }

    private async Task<int> EditAsync(EditRequest request, string text, CancellationToken cancellationToken) {
        EditorRunner editor = new(seams.Environment, paths.StateDir(), seams.Process, logger);
        string current = text;
        while (true) {
            string edited;
            try {
                edited = await editor.EditAsync(current, request.Suffix, cancellationToken).ConfigureAwait(false);
            } catch (EditorException failed) {
                return await FailAsync(failed).ConfigureAwait(false);
            }
            SaveResult saved;
            try {
                saved = request.Save(edited);
            } catch (ConfigInvalidException invalid) {
                if (!await AsksAgainAsync(invalid).ConfigureAwait(false)) {
                    await output.WriteLineAsync(request.KeptLine).ConfigureAwait(false);
                    return CliExitCodes.Ok;
                }
                current = edited;
                continue;
            } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
                LogEvents.EditFailed(logger, SaveFailure);
                await output.WriteLineAsync(messages.SaveFailed(request.TargetPath)).ConfigureAwait(false);
                return CliExitCodes.Failure;
            }
            await WriteSavedAsync(output, messages, request.SavedLine(saved), saved.Estimate).ConfigureAwait(false);
            return CliExitCodes.Ok;
        }
    }

    private async Task<bool> AsksAgainAsync(ConfigInvalidException invalid) {
        foreach (ConfigError error in invalid.Errors) {
            await output.WriteLineAsync(error.Describe(messages)).ConfigureAwait(false);
        }
        await output.WriteAsync(messages.EditAgain).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
        return seams.ReadLine() is string answer
            && !messages.EditNoAnswers.Contains(answer.Trim(), StringComparer.OrdinalIgnoreCase);
    }

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
