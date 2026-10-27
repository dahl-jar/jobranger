using System.Collections.Concurrent;
using System.Text;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeChildProcessRunner(Func<ChildRequest, Task<ChildExit>> reply) : IChildProcessRunner {
    public FakeChildProcessRunner(string output)
        : this(_ => Task.FromResult(new ChildExit(0, Encoding.UTF8.GetBytes(output), TimedOut: false, OutputTooLarge: false))) {
    }

    public ConcurrentQueue<ChildRequest> Requests { get; } = new();

    public Task<ChildExit> RunAsync(ChildRequest request, CancellationToken cancellationToken) {
        Requests.Enqueue(request);
        return reply(request);
    }
}
