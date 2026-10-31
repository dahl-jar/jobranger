using Jobranger.Cli.Features.Runs;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FailingRunEvents(int failingEvent) : IRunEvents {
    private int emitted;

    public void Emit(RunEvent runEvent) {
        if (Interlocked.Increment(ref emitted) == failingEvent) {
            throw new InvalidOperationException("run event sink failed");
        }
    }
}
