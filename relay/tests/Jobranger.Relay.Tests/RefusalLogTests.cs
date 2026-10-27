using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Relay.Tests;

public sealed class RefusalLogTests(RelayFactory factory) : IClassFixture<RelayFactory> {
    private const int claimBurst = 5;
    private const int extraClaims = 3;

    [Fact]
    public async Task ShouldLogRateLimitRefusalAtMostOncePerSecondPerPolicy() {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        await api.Claims(claimBurst + extraClaims);
        int sameSecond = RefusalLines();
        factory.Time.Advance(TimeSpan.FromSeconds(1));
        await api.Claims(1);

        int nextSecond = RefusalLines();

        Assert.Equal((1, 2), (sameSecond, nextSecond));
    }

    [Fact]
    public async Task ShouldLogRejectionOncePerSecondPerReason() {
        using WebApplicationFactory<Program> isolated = factory.WithWebHostBuilder(_ => { });
        using RelayApi api = RelayApi.For(isolated, Addresses.Next());
        FakeLogCollector logs = isolated.Services.GetFakeLogCollector();
        await api.Claims(extraClaims);
        int sameSecond = RejectionLines(logs).Count;
        factory.Time.Advance(TimeSpan.FromSeconds(1));
        await api.Claims(1);

        List<FakeLogRecord> lines = RejectionLines(logs);

        Assert.Equal((1, 2), (sameSecond, lines.Count));
        Assert.All(lines, line => Assert.Equal(["Reason", "{OriginalFormat}"], line.StructuredState!.Select(pair => pair.Key)));
    }

    private static List<FakeLogRecord> RejectionLines(FakeLogCollector logs) =>
        [.. logs.GetSnapshot().Where(record => record.Message == "request rejected reason=wrong_code")];

    private int RefusalLines() =>
        factory.Logs.GetSnapshot().Count(record => record.Message == "rate limit refused policy=pair-claim");
}
