using System.Collections;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Platform;

[UnsupportedOSPlatform("windows")]
internal sealed class EditorRunner(IDictionary environment, string stateDir, IEditorProcess process, ILogger logger) {
    private const string visualVariable = "VISUAL";
    private const string editorVariable = "EDITOR";
    private const string fallbackEditor = "vi";
    private const string folderPrefix = "edit-";
    private const string fileStem = "edit";

    private static readonly EditorCommand fallback = new(fallbackEditor, []);

    public async Task<string> EditAsync(string text, string suffix, CancellationToken cancellationToken) {
        EditorCommand editor = Choose();
        string folder = Path.Combine(stateDir, folderPrefix + Path.GetRandomFileName());
        string file = Path.Combine(folder, fileStem + suffix);
        try {
            WriteFile(file, text, editor.Program);
            await OpenAsync(editor, file, cancellationToken).ConfigureAwait(false);
            return ReadFile(file, editor.Program);
        } finally {
            RemoveFolder(folder);
        }
    }

    private EditorCommand Choose() =>
        Parsed(visualVariable) ?? Parsed(editorVariable) ?? fallback;

    private EditorCommand? Parsed(string variable) =>
        EnvironmentVariables.Read(environment, variable) is string value ? EditorCommand.Parse(value) : null;

    private void WriteFile(string file, string text, string program) {
        try {
            PrivateFile.Write(file, Encoding.UTF8.GetBytes(text), logger);
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            throw new EditorException(EditorProblem.TempFile, program);
        }
    }

    private async Task OpenAsync(EditorCommand editor, string file, CancellationToken cancellationToken) {
        int exit;
        try {
            exit = await process.RunAsync(editor.Program, [.. editor.Arguments, file], cancellationToken)
                .ConfigureAwait(false);
        } catch (Win32Exception) {
            throw new EditorException(EditorProblem.NotStarted, editor.Program);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw new EditorException(EditorProblem.FailedExit, editor.Program);
        }
        if (exit != 0) {
            throw new EditorException(EditorProblem.FailedExit, editor.Program);
        }
    }

    private static string ReadFile(string file, string program) {
        try {
            return ConfigFile.ReadText(file);
        } catch (Exception error) when (error is ConfigMissingException or ConfigInvalidException) {
            throw new EditorException(EditorProblem.TempFile, program);
        }
    }

    private void RemoveFolder(string folder) {
        try {
            if (Directory.Exists(folder)) {
                Directory.Delete(folder, recursive: true);
            }
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            LogEvents.EditCleanupFailed(logger, Path.GetFileName(folder), error.GetType().Name);
        }
    }
}
