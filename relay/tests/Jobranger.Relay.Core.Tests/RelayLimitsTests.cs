namespace Jobranger.Relay.Core.Tests;

public sealed class RelayLimitsTests
{
    private const long podMemoryLimitBytes = 512L * 1024 * 1024;
    private const long peakShareOfPodLimitPercent = 80;
    private const long measuredStoresBytes = 133L * 1024 * 1024;
    private const long measuredHubSocketBytes = 820L * 1024 * 1024 / 1000;
    private const long marginPercent = 105;

    [Fact]
    public void ShouldFitHubAtCapWithFullStoresInPeakBudget()
    {
        long peak = RelayLimits.RelayBaselineBytes + RelayLimits.StoresBudgetBytes
            + ((long)RelayLimits.MaxHubSockets * RelayLimits.HubSocketBudgetBytes);

        Assert.InRange(peak, 0, podMemoryLimitBytes * peakShareOfPodLimitPercent / 100);
    }

    [Fact]
    public void ShouldUseWholeBudgetForHubSockets()
    {
        long oneMore = RelayLimits.RelayBaselineBytes + RelayLimits.StoresBudgetBytes
            + ((long)(RelayLimits.MaxHubSockets + 1) * RelayLimits.HubSocketBudgetBytes);

        Assert.True(oneMore > podMemoryLimitBytes * peakShareOfPodLimitPercent / 100);
    }

    [Fact]
    public void ShouldCoverMeasuredStoresWithMargin()
    {
        long stores = RelayLimits.StoresBudgetBytes;

        Assert.True(stores * 100 >= measuredStoresBytes * marginPercent);
    }

    [Fact]
    public void ShouldCoverMeasuredSocketCostWithMargin()
    {
        long perSocket = RelayLimits.HubSocketBudgetBytes;

        Assert.True(perSocket * 100 >= measuredHubSocketBytes * marginPercent);
    }

    [Fact]
    public void ShouldBufferOneFullFrameFromClient()
    {
        int applicationBuffer = RelayLimits.HubApplicationBufferBytes;

        Assert.True(applicationBuffer >= RelayLimits.MaxFrameBytes);
    }
}
