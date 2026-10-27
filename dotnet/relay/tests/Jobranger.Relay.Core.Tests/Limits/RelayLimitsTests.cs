using System.Buffers.Text;
using System.Security.Cryptography;
using Jobranger.Messaging.Channels;
using Jobranger.Relay.Core.Limits;
using Jobranger.Relay.Core.Nonces;
using Jobranger.Relay.Core.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests.Limits;

[Collection(typeof(MemoryMeasurement))]
public sealed class RelayLimitsTests {
    private const long podMemoryLimitBytes = 512L * 1024 * 1024;
    private const long peakShareOfPodLimitPercent = 80;
    private const long measuredStoresBytes = 133L * 1024 * 1024;
    private const long measuredStoresNonceBytes = 29L * 1024 * 1024;
    private const long measuredHubSocketBytes = 820L * 1024 * 1024 / 1000;
    private const long marginPercent = 105;

    [Fact]
    public void ShouldFitHubAtCapWithFullStoresInPeakBudget() {
        long peak = RelayLimits.RelayBaselineBytes + RelayLimits.StoresBudgetBytes
            + ((long)RelayLimits.MaxHubSockets * RelayLimits.HubSocketBudgetBytes);

        Assert.InRange(peak, 0, podMemoryLimitBytes * peakShareOfPodLimitPercent / 100);
    }

    [Fact]
    public void ShouldUseWholeBudgetForHubSockets() {
        long oneMore = RelayLimits.RelayBaselineBytes + RelayLimits.StoresBudgetBytes
            + ((long)(RelayLimits.MaxHubSockets + 1) * RelayLimits.HubSocketBudgetBytes);

        Assert.True(oneMore > podMemoryLimitBytes * peakShareOfPodLimitPercent / 100);
    }

    [Fact]
    public void ShouldCoverMeasuredStoresAndNonceBudget() {
        long stores = RelayLimits.StoresBudgetBytes;

        Assert.True(stores >= measuredStoresBytes - measuredStoresNonceBytes + RelayLimits.NonceStoreBudgetBytes);
    }

    [Fact]
    public void ShouldFitFullNonceStoreWithTombstonesInNonceBudget() {
        FakeTimeProvider time = new();
        long before = GC.GetTotalMemory(true);
        NonceStore store = new(time);

        int issued = FillWithOwnNetworkChannelNonces(store, 0);
        time.Advance(RelayLimits.NonceLifetime);
        store.Sweep();
        issued += FillWithOwnNetworkChannelNonces(store, RelayLimits.MaxOutstandingNonces);
        long held = GC.GetTotalMemory(true) - before;
        GC.KeepAlive(store);

        Assert.Equal(RelayLimits.MaxOutstandingNonces * 2, issued);
        Assert.InRange(held * marginPercent, 0, RelayLimits.NonceStoreBudgetBytes * 100);
    }

    [Fact]
    public void ShouldCoverMeasuredSocketCostWithMargin() {
        long perSocket = RelayLimits.HubSocketBudgetBytes;

        Assert.True(perSocket * 100 >= measuredHubSocketBytes * marginPercent);
    }

    [Fact]
    public void ShouldBufferOneFullFrameFromClient() {
        int applicationBuffer = RelayLimits.HubApplicationBufferBytes;

        Assert.True(applicationBuffer >= RelayLimits.MaxFrameBytes);
    }

    private static int FillWithOwnNetworkChannelNonces(NonceStore store, int first) {
        int issued = 0;
        for (int index = first; index < first + RelayLimits.MaxOutstandingNonces; index++) {
            ChannelId channel = new(Base64Url.EncodeToString(SHA256.HashData(BitConverter.GetBytes(index))));
            if (store.IssueForChannel(channel, $"2001:db8:{index:x}:1::/64", $"2001:db8:{index:x}::/48") is not null) {
                issued++;
            }
        }

        return issued;
    }
}
