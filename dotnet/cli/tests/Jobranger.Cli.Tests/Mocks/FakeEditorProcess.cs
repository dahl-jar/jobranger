using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeEditorProcess(Func<string, int> edit) : IEditorProcess {
    public List<(string Program, IReadOnlyList<string> Arguments)> Starts { get; } = [];

    public Task<int> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken) {
        Starts.Add((program, arguments));
        return Task.FromResult(edit(arguments[^1]));
    }
}
