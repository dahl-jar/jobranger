using Jobranger.Cli.Features.Cv;

namespace Jobranger.Cli.Commands;

internal static class CvWorkerCommand {
    private const int argumentCount = 2;

    public static bool Matches(IReadOnlyList<string> args) => args.Count == argumentCount && args[0] == Cv.WorkerCommandName;

    public static async Task<int> RunAsync(string suffix) {
        Stream input = Console.OpenStandardInput();
        await using (input.ConfigureAwait(false)) {
            Stream output = Console.OpenStandardOutput();
            await using (output.ConfigureAwait(false)) {
                await Cv.RunWorkerAsync(suffix, input, output).ConfigureAwait(false);
            }
        }
        return CliExitCodes.Ok;
    }
}
