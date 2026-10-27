using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Relay.Tests.Mocks;

internal static class LogText {
    public static string Written(IEnumerable<FakeLogRecord> records) => string.Join('\n', records.Select(Written));

    private static string Written(FakeLogRecord record) =>
        $"{record.Message} {string.Join(' ', record.StructuredState?.Select(pair => $"{pair.Key}={pair.Value}") ?? [])}";
}
