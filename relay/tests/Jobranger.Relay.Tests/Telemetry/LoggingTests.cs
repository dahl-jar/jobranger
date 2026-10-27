using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Jobranger.Relay.Tests.Mocks;
using Microsoft.Extensions.Logging.Testing;

namespace Jobranger.Relay.Tests.Telemetry;

public sealed partial class LoggingTests(RelayFactory factory) : IClassFixture<RelayFactory> {
    [Fact]
    public async Task ShouldNotLogCodesHandlesOrRawChannelIds() {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        using CliKey key = new();
        string nonce = await api.Nonce();
        using HttpResponseMessage startResponse = await api.Post("/pair/start", key.Proof(CliKey.PairStart, nonce));
        JsonObject started = await RelayApi.Json(startResponse);
        string code = (string)started["code"]!;
        using HttpResponseMessage wrong = await api.Claim(Codes.WithWrongVerifier(code));
        using HttpResponseMessage claimResponse = await api.Claim(code);
        JsonObject claimed = await RelayApi.Json(claimResponse);
        string sessionNonce = await api.Nonce();
        using HttpResponseMessage cliResponse = await api.Post("/session/cli", key.Proof(CliKey.SessionCli, sessionNonce));
        JsonObject cliSession = await RelayApi.Json(cliResponse);
        using HttpResponseMessage webResponse = await api.WebSession((string)claimed["ticket"]!);
        JsonObject webSession = await RelayApi.Json(webResponse);
        string[] secrets =
        [
            code, code.Replace("-", string.Empty, StringComparison.Ordinal), code[..4], code[5..], nonce, key.PublicKey,
            (string)started["channel"]!, (string)started["handle"]!, (string)claimed["handle"]!, (string)claimed["ticket"]!,
            sessionNonce, (string)cliSession["handle"]!, (string)webSession["handle"]!, (string)webSession["ticket"]!,
        ];

        IReadOnlyList<FakeLogRecord> records = factory.Logs.GetSnapshot();

        string written = string.Join('\n', records.Select(Written));
        Assert.Contains(records, record => record.Message.StartsWith("pairing started", StringComparison.Ordinal));
        Assert.Contains(records, record => record.Message.StartsWith("pairing claimed", StringComparison.Ordinal));
        Assert.Contains(records, record => record.Message.StartsWith("session started role=Web", StringComparison.Ordinal));
        Assert.DoesNotContain(secrets, secret => written.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldLogChannelAsSameKeyedPseudonymAcrossEvents() {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        using CliKey key = new();
        await api.Paired(key);

        IReadOnlyList<FakeLogRecord> records = factory.Logs.GetSnapshot();

        string started = Pseudonym(records, "pairing started");
        Assert.Matches(KeyedPseudonym(), started);
        Assert.Equal(started, Pseudonym(records, "pairing claimed"));
    }

    [GeneratedRegex("^1:[A-Za-z0-9+/]{22}==$")]
    private static partial Regex KeyedPseudonym();

    private static string Pseudonym(IReadOnlyList<FakeLogRecord> records, string message) =>
        records.Last(record => record.Message.StartsWith(message, StringComparison.Ordinal))
            .StructuredState!.Single(pair => pair.Key == "Channel").Value!;

    private static string Written(FakeLogRecord record) =>
        $"{record.Message} {string.Join(' ', record.StructuredState?.Select(pair => $"{pair.Key}={pair.Value}") ?? [])}";
}
