using Jobranger.Cli.Features.Link;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Cli.Tests.Features.Link;

public sealed class SendPacerTests {
    private const int bytesPerSecond = 1000;

    [Fact]
    public async Task ShouldHoldNextFrame_WhenLastFrameUsedSecondOfBudget() {
        FakeTimeProvider time = new();
        SendPacer pacer = new(time, bytesPerSecond);
        Task first = pacer.WaitAsync(bytesPerSecond, TestContext.Current.CancellationToken);

        Task next = pacer.WaitAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal((true, false), (first.IsCompleted, next.IsCompleted));
        time.Advance(TimeSpan.FromSeconds(1));
        await next.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
}
