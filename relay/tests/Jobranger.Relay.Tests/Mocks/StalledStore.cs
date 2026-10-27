using Jobranger.Relay.Core.Sweeping;

namespace Jobranger.Relay.Tests.Mocks;

internal sealed class StalledStore : ISweepable, IDisposable {
    private static readonly TimeSpan wait = TimeSpan.FromSeconds(10);

    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => entered.Task.WaitAsync(wait, TestContext.Current.CancellationToken);

    public void Sweep() {
        entered.TrySetResult();
        released.Task.Wait();
    }

    public void Dispose() => released.TrySetResult();
}
