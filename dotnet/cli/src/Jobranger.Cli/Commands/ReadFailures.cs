using Jobranger.Cli.Files;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Commands;

internal static class ReadFailures {
    private const string outputFailed = "output_failed";

    public static int PrintLines(
        Func<IReadOnlyList<string>> read,
        Action<string> logFailure,
        Paths paths,
        IMessages messages,
        TextWriter output) {
        IReadOnlyList<string> lines;
        int exit = CliExitCodes.Ok;
        try {
            lines = read();
        } catch (Exception error) when (CommandFailure.Of(error, paths, messages) is ReadFailure failure) {
            logFailure(failure.Code);
            lines = failure.Lines;
            exit = CliExitCodes.Failure;
        }
        try {
            foreach (string line in lines) {
                output.WriteLine(line);
            }
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            logFailure(outputFailed);
            return CliExitCodes.Failure;
        }
        return exit;
    }
}
