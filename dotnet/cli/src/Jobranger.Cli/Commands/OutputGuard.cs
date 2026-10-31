using System.Text;
using Spectre.Console;

namespace Jobranger.Cli.Commands;

internal static class OutputGuard {
    public static async Task<int> RunAsync(IAnsiConsole console, Action<string> logFailure, Func<Task<int>> action) {
        IAnsiConsoleOutput original = console.Profile.Out;
        console.Profile.Out = new GuardedOutput(original);
        try {
            return await action().ConfigureAwait(false);
        } catch (OutputFailedException) {
            logFailure(ReadFailures.OutputFailed);
            return CliExitCodes.Failure;
        } finally {
            console.Profile.Out = original;
        }
    }

    private sealed class OutputFailedException(Exception cause) : Exception(cause.Message, cause);

    private sealed class GuardedOutput(IAnsiConsoleOutput inner) : IAnsiConsoleOutput {
        public TextWriter Writer { get; } = new GuardedWriter(inner.Writer);

        public bool IsTerminal => inner.IsTerminal;

        public int Width => inner.Width;

        public int Height => inner.Height;

        public void SetEncoding(Encoding encoding) => inner.SetEncoding(encoding);
    }

    private sealed class GuardedWriter(TextWriter inner) : TextWriter {
        public override Encoding Encoding => inner.Encoding;

        public override void Write(char value) => Attempt(() => inner.Write(value));

        public override void Write(string? value) => Attempt(() => inner.Write(value));

        public override void WriteLine(string? value) => Attempt(() => inner.WriteLine(value));

        public override void Flush() => Attempt(inner.Flush);

        public override async Task WriteLineAsync(string? value) {
            try {
                await inner.WriteLineAsync(value).ConfigureAwait(false);
            } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
                throw new OutputFailedException(error);
            }
        }

        private static void Attempt(Action write) {
            try {
                write();
            } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
                throw new OutputFailedException(error);
            }
        }
    }
}
