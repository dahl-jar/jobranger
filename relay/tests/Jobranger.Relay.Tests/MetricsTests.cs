using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json.Nodes;
using Jobranger.Contracts.Channels;
using Jobranger.Relay.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Jobranger.Relay.Tests;

public sealed class MetricsTests(RelayFactory factory) : IClassFixture<RelayFactory> {
    private const string meterName = "Jobranger.Relay";
    private const string metricsPath = "/metrics";
    private const string healthPath = "/healthz";
    private const string unknownCode = "BBBB-BBBB";
    private const int claimBurst = 5;
    private const int requestsPerChannel = 30;
    private const string kestrelMeter = "Microsoft.AspNetCore.Server.Kestrel";
    private const string kestrelInstrument = "kestrel.test_connections";
    private const string localAddress = "192.0.2.7";

    [Fact]
    public async Task ShouldServeMetricsOnMetricsPort() {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await Scrape(client, metricsPath, LocalPortFilter.MetricsPort);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ShouldNotServeMetricsWhenHostHeaderNamesMetricsPort() {
        using HttpClient client = factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, metricsPath);
        request.Headers.Host = $"relay.example.test:{LocalPortFilter.MetricsPort}";

        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShouldAnswerNotFoundForHealthOnMetricsPort() {
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await Scrape(client, healthPath, LocalPortFilter.MetricsPort);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ShouldCountStartedPairing() {
        using MetricCollector<long> started = Collector("relay.pairings.started");
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        using CliKey key = new();

        await api.StartPairing(key);

        Assert.Equal(1, Total(started));
    }

    [Fact]
    public async Task ShouldCountClaimedPairing() {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        using CliKey key = new();
        JsonObject started = await api.StartPairing(key);
        using MetricCollector<long> claimed = Collector("relay.pairings.claimed");

        using HttpResponseMessage response = await api.Claim((string)started["code"]!);

        Assert.Equal(1, Total(claimed));
    }

    [Fact]
    public async Task ShouldCountRejectedRequestsByCode() {
        using MetricCollector<long> rejected = Collector("relay.requests.rejected");
        using RelayApi api = RelayApi.For(factory, Addresses.Next());

        using HttpResponseMessage response = await api.Claim(unknownCode);

        Assert.Equal([("wrong_code", 1L)], ByTag(rejected, "code"));
    }

    [Fact]
    public async Task ShouldCountRateLimitedRequestsByPolicy() {
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        await api.Claims(claimBurst);
        using MetricCollector<long> limited = Collector("relay.rate_limited");

        using HttpResponseMessage response = await api.Claim(unknownCode);

        Assert.Equal([("pair-claim", 1L)], ByTag(limited, "policy"));
    }

    [Fact]
    public async Task ShouldCountRefusedChannelNonceUnderItsOwnPolicy() {
        using CliKey key = new();
        using RelayApi owner = RelayApi.For(factory, Addresses.Next());
        await owner.Paired(key);
        using RelayApi api = RelayApi.For(factory, Addresses.Next());
        for (int request = 0; request < requestsPerChannel; request++) {
            using HttpResponseMessage admitted = await api.Post("/pair/nonce", RelayApi.NonceBody(key.Channel.Value));
        }

        using MetricCollector<long> limited = Collector("relay.rate_limited");

        using HttpResponseMessage response = await api.Post("/pair/nonce", RelayApi.NonceBody(key.Channel.Value));

        Assert.Equal([("channel-nonce", 1L)], ByTag(limited, "policy"));
    }

    [Fact]
    public async Task ShouldCountForwardedEnvelopes() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer cli = await RelayClients.ConnectCliAsync(factory, channel);
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        JsonObject envelope = Envelopes.Data(channel, Envelopes.NewSid());
        using MetricCollector<long> forwarded = Collector("relay.envelopes.forwarded");
        using MetricCollector<long> chars = Collector("relay.envelopes.chars");

        await browser.Send(envelope.ToJsonString());
        await cli.Next(HubPeer.Receive);

        Assert.Equal((1L, (long)((string)envelope["ciphertext"]!).Length), (Total(forwarded), Total(chars)));
    }

    [Fact]
    public async Task ShouldCountRejectedEnvelopesByReason() {
        ChannelId channel = RelayClients.NewChannel();
        await using HubPeer browser = await RelayClients.ConnectWebAsync(factory, channel);
        JsonObject envelope = Envelopes.WithUnsupportedVersion(channel);
        using MetricCollector<long> rejected = Collector("relay.envelopes.rejected");

        await browser.Rejection(envelope.ToJsonString());

        Assert.Equal([("unsupported_version", 1L)], ByTag(rejected, "reason"));
    }

    [Fact]
    public async Task ShouldCountRefusedHubJoinsByReason() {
        string handle = RelayClients.Handle(factory, RelayClients.NewChannel(), RelayRole.Web);
        await using HubPeer first = await RelayClients.ConnectAsync(factory, handle, RelayFactory.SiteOrigin);
        await using HubPeer second = await RelayClients.ConnectAsync(factory, handle, RelayFactory.SiteOrigin);
        using MetricCollector<long> refused = Collector("relay.connections.rejected");
        await using HubPeer third = RelayClients.Build(factory, handle, RelayFactory.SiteOrigin);
        await third.Connection.StartAsync(TestContext.Current.CancellationToken);

        await third.Closed;

        Assert.Equal([("ticket_full", 1L)], ByTag(refused, "reason"));
    }

    [Fact]
    public async Task ShouldCountRefusedHubAuthByReason() {
        using MetricCollector<long> refused = Collector("relay.connections.rejected");
        await using HubPeer peer = RelayClients.Build(factory, "not-a-handle", null);

        await Assert.ThrowsAnyAsync<Exception>(() => peer.Connection.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal([("invalid_handle", 1L)], ByTag(refused, "reason"));
    }

    [Fact]
    public async Task ShouldReportActiveChannels() {
        await using RelayFactory own = new();
        await using HubPeer cli = await RelayClients.ConnectCliAsync(own, RelayClients.NewChannel());
        using MetricCollector<int> active = new(own.Services.GetRequiredService<IMeterFactory>(), meterName, "relay.channels.active");

        active.RecordObservableInstruments();

        Assert.Equal(1, active.LastMeasurement?.Value);
    }

    [Fact]
    public async Task ShouldKeepIdentifiersOutOfMetrics() {
        string address = Addresses.Next();
        using RelayApi api = RelayApi.For(factory, address);
        using CliKey key = new();
        JsonObject started = await api.StartPairing(key);
        using HttpResponseMessage claimResponse = await api.Claim((string)started["code"]!);
        JsonObject claimed = await RelayApi.Json(claimResponse);
        ChannelId channel = new((string)started["channel"]!);
        await using HubPeer cli = await RelayClients.ConnectAsync(factory, (string)started["handle"]!, null, address);
        await using HubPeer browser = await RelayClients.ConnectAsync(factory, (string)claimed["handle"]!, RelayFactory.SiteOrigin);
        await browser.Send(Envelopes.Data(channel, Envelopes.NewSid()).ToJsonString());
        await cli.Next(HubPeer.Receive);
        string[] identifiers =
        [
            channel.Value, address, key.PublicKey, (string)started["code"]!, (string)started["handle"]!,
            (string)claimed["handle"]!, (string)claimed["ticket"]!,
        ];
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await Scrape(client, metricsPath, LocalPortFilter.MetricsPort);

        string scraped = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("relay_pairings_started", scraped, StringComparison.Ordinal);
        Assert.DoesNotContain(identifiers, identifier => scraped.Contains(identifier, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldDropServerAddressFromKestrelMetrics() {
        using Meter kestrel = factory.Services.GetRequiredService<IMeterFactory>().Create(kestrelMeter);
        kestrel.CreateCounter<long>(kestrelInstrument).Add(
            1, new KeyValuePair<string, object?>("server.address", localAddress), new KeyValuePair<string, object?>("server.port", LocalPortFilter.HttpPort));
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await Scrape(client, metricsPath, LocalPortFilter.MetricsPort);

        string scraped = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal((true, false), (scraped.Contains($"server_port=\"{LocalPortFilter.HttpPort}\"", StringComparison.Ordinal), scraped.Contains(localAddress, StringComparison.Ordinal)));
    }

    private static Task<HttpResponseMessage> Scrape(HttpClient client, string path, int port) {
        HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add(LocalPortFilter.PortHeader, port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static long Total(MetricCollector<long> collector) => collector.GetMeasurementSnapshot().Sum(measurement => measurement.Value);

    private static (string, long)[] ByTag(MetricCollector<long> collector, string tag) =>
    [
        .. collector.GetMeasurementSnapshot()
            .GroupBy(measurement => (string?)measurement.Tags[tag] ?? string.Empty)
            .Select(group => (group.Key, group.Sum(measurement => measurement.Value))),
    ];

    private MetricCollector<long> Collector(string instrument) =>
        new(factory.Services.GetRequiredService<IMeterFactory>(), meterName, instrument);
}
