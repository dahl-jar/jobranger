using System.Collections.Concurrent;
using Jobranger.Cli.Features.Runs;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class RecordingRunEvents : IRunEvents {
    private readonly ConcurrentQueue<RunEvent> events = new();

    public IReadOnlyList<RunEvent> Events => [.. events];

    public void Emit(RunEvent runEvent) => events.Enqueue(runEvent);
}
