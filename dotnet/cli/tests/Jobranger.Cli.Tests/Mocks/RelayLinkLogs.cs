using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Cli.Tests.Mocks;

internal static class RelayLinkLogs {
    private const string connectedEvent = "link.relay.connected";

    public static Task ConnectedAsync(FakeLogger logger) =>
        TestWaits.UntilAsync(() => logger.Collector.GetSnapshot().Any(record => record.Id.Name == connectedEvent));
}
