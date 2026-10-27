namespace Jobranger.Cli.Logging;

internal static class RunScope {
    private static readonly AsyncLocal<string?> runId = new();

    public static string? Current => runId.Value;

    public static IDisposable Bind(string runId) {
        string? previous = RunScope.runId.Value;
        RunScope.runId.Value = runId;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable {
        public void Dispose() => runId.Value = previous;
    }
}
