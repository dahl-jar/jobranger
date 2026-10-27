using System.Text.RegularExpressions;
using Jobranger.Relay.Core.Tokens;
using Jobranger.Relay.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Relay.Tests.Telemetry;

public sealed partial class RequestLogTests {
    [Theory]
    [InlineData(RelayFactory.DefaultLogKey)]
    [InlineData("Logging:LogLevel:Microsoft.AspNetCore.Hosting.Diagnostics")]
    [InlineData("Logging:Fake:LogLevel:Default")]
    public async Task ShouldKeepHandleOutOfLogsWhenConfigAllowsDebug(string debugKey) {
        using RelayFactory factory = new() { DebugLogKey = debugKey };
        string handle = RelayClients.Handle(factory, RelayClients.NewChannel(), RelayRole.Cli);

        await using HubPeer peer = await RelayClients.ConnectAsync(factory, handle, null);

        IReadOnlyList<FakeLogRecord> records = factory.Logs.GetSnapshot();
        string written = LogText.Written(records);
        Assert.Contains(records, record => record.Level == LogLevel.Debug);
        Assert.DoesNotContain(handle, written, StringComparison.Ordinal);
        Assert.DoesNotMatch(TokenValue(), written);
    }

    [GeneratedRegex("access_token=[^&\\s]")]
    private static partial Regex TokenValue();
}
