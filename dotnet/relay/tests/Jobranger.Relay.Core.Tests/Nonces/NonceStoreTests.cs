using Jobranger.Messaging.Channels;
using Jobranger.Relay.Core.Nonces;
using Jobranger.Relay.Core.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests.Nonces;

public sealed class NonceStoreTests {
    private static readonly TimeSpan nonceLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan justBeforeExpiry = TimeSpan.FromSeconds(59);
    private static readonly TimeSpan tombstoneRetention = TimeSpan.FromMinutes(5);
    private const int nonceChars = 43;
    private const string networkA = "198.51.100.0/24";
    private const string networkB = "203.0.113.0/24";
    private const string requesterA = "198.51.100.7";
    private const string requesterB = "198.51.100.8";
    private const string requesterC = "198.51.100.9";
    private const string otherNetworkRequester = "203.0.113.7";

    private readonly FakeTimeProvider time = new();
    private readonly NonceStore store;

    public NonceStoreTests() {
        store = new NonceStore(time);
    }

    [Fact]
    public void ShouldIssueNonceOf32BytesValidForSixtySeconds() {
        RelayNonce nonce = Issued(store);

        Assert.Equal((nonceChars, time.GetUtcNow() + nonceLifetime), (nonce.Value.Length, nonce.ExpiresAt));
    }

    [Fact]
    public void ShouldIssueDifferentNonces() {
        RelayNonce first = Issued(store);

        RelayNonce second = Issued(store);

        Assert.NotEqual(first.Value, second.Value);
    }

    [Fact]
    public void ShouldConsumeNonceOnlyOnce() {
        RelayNonce nonce = Issued(store);
        store.Consume(nonce.Value);

        NonceOutcome second = store.Consume(nonce.Value);

        Assert.Equal(NonceOutcome.Unknown, second);
    }

    [Fact]
    public void ShouldAcceptNonceBeforeSixtySeconds() {
        RelayNonce nonce = Issued(store);
        time.Advance(justBeforeExpiry);

        NonceOutcome outcome = store.Consume(nonce.Value);

        Assert.Equal(NonceOutcome.Valid, outcome);
    }

    [Fact]
    public void ShouldReportNonceExpiredAfterSixtySeconds() {
        RelayNonce nonce = Issued(store);
        time.Advance(nonceLifetime);

        NonceOutcome outcome = store.Consume(nonce.Value);

        Assert.Equal(NonceOutcome.Expired, outcome);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8")]
    public void ShouldReportNonceNeverIssuedAsUnknown(string? nonce) {
        Issued(store);

        NonceOutcome outcome = store.Consume(nonce);

        Assert.Equal(NonceOutcome.Unknown, outcome);
    }

    [Fact]
    public void ShouldReportNonceExpiredAfterLaterIssueDrainsIt() {
        RelayNonce nonce = Issued(store);
        time.Advance(nonceLifetime);
        Issued(store);

        NonceOutcome outcome = store.Consume(nonce.Value);

        Assert.Equal(NonceOutcome.Expired, outcome);
    }

    [Fact]
    public void ShouldForgetExpiredNonceOnceTombstoneRetentionPasses() {
        RelayNonce nonce = Issued(store);
        time.Advance(nonceLifetime + tombstoneRetention);
        Issued(store);

        NonceOutcome outcome = store.Consume(nonce.Value);

        Assert.Equal(NonceOutcome.Unknown, outcome);
    }

    [Fact]
    public void ShouldForgetExpiredNonceOnSweepAfterTombstoneRetention() {
        RelayNonce nonce = Issued(store);
        time.Advance(nonceLifetime + tombstoneRetention);
        store.Sweep();

        NonceOutcome outcome = store.Consume(nonce.Value);

        Assert.Equal(NonceOutcome.Unknown, outcome);
    }

    [Fact]
    public void ShouldKeepExpiredNonceUntilTombstoneRetentionPasses() {
        RelayNonce nonce = Issued(store);
        time.Advance(nonceLifetime + tombstoneRetention - TimeSpan.FromSeconds(1));
        Issued(store);

        NonceOutcome outcome = store.Consume(nonce.Value);

        Assert.Equal(NonceOutcome.Expired, outcome);
    }

    [Fact]
    public void ShouldReportConsumedNonceUnknownAfterItsExpiry() {
        RelayNonce nonce = Issued(store);
        store.Consume(nonce.Value);
        time.Advance(nonceLifetime);
        Issued(store);

        NonceOutcome outcome = store.Consume(nonce.Value);

        Assert.Equal(NonceOutcome.Unknown, outcome);
    }

    [Fact]
    public void ShouldRefuseIssueWhenOutstandingLimitIsReached() {
        NonceStore small = new(time, maxOutstanding: 2);
        Issued(small);
        Issued(small);

        RelayNonce? third = small.IssueForNetwork(networkA);

        Assert.Null(third);
    }

    [Fact]
    public void ShouldIssueAgainOnceOutstandingNoncesExpire() {
        NonceStore small = new(time, maxOutstanding: 1);
        Issued(small);
        time.Advance(nonceLifetime);

        RelayNonce? next = small.IssueForNetwork(networkA);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldIssueWhileConsumedNoncesStayUnderLimit() {
        const int limit = 3;
        const int rounds = limit * 10;
        NonceStore small = new(time, maxOutstanding: limit);

        int refused = Enumerable.Range(0, rounds).Count(_ => small.IssueForNetwork(networkA) is not { } nonce || small.Consume(nonce.Value) != NonceOutcome.Valid);

        Assert.Equal(0, refused);
    }

    [Fact]
    public void ShouldRefuseNetworkAtItsLimit() {
        NonceStore shared = new(time, maxOutstanding: 10, maxPerNetwork: 2);
        shared.IssueForNetwork(networkA);
        shared.IssueForNetwork(networkA);

        (bool, bool) outcomes = (shared.IssueForNetwork(networkA) is null, shared.IssueForNetwork(networkB) is null);

        Assert.Equal((true, false), outcomes);
    }

    [Fact]
    public void ShouldFreeNetworkShareOnceItsNonceIsConsumed() {
        NonceStore shared = new(time, maxOutstanding: 10, maxPerNetwork: 1);
        RelayNonce nonce = shared.IssueForNetwork(networkA)!;
        shared.Consume(nonce.Value);

        RelayNonce? next = shared.IssueForNetwork(networkA);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldFreeNetworkShareOnceItsNonceExpires() {
        NonceStore shared = new(time, maxOutstanding: 10, maxPerNetwork: 1);
        shared.IssueForNetwork(networkA);
        time.Advance(nonceLifetime);

        RelayNonce? next = shared.IssueForNetwork(networkA);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldFreeOneNetworkShareForEachConsumedNonce() {
        NonceStore shared = new(time, maxOutstanding: 10, maxPerNetwork: 2);
        RelayNonce first = shared.IssueForNetwork(networkA)!;
        shared.IssueForNetwork(networkA);
        shared.Consume(first.Value);

        (bool, bool) outcomes = (shared.IssueForNetwork(networkA) is null, shared.IssueForNetwork(networkA) is null);

        Assert.Equal((false, true), outcomes);
    }

    [Fact]
    public void ShouldDropOldestTombstoneOnceTombstonesExceedOutstandingLimit() {
        NonceStore small = new(time, maxOutstanding: 1);
        RelayNonce oldest = Issued(small);
        time.Advance(nonceLifetime);
        RelayNonce newer = Issued(small);
        time.Advance(nonceLifetime);
        Issued(small);

        (NonceOutcome, NonceOutcome) outcomes = (small.Consume(oldest.Value), small.Consume(newer.Value));

        Assert.Equal((NonceOutcome.Unknown, NonceOutcome.Expired), outcomes);
    }

    [Fact]
    public void ShouldIssueChannelNoncesOutsideNetworkLimit() {
        NonceStore shared = new(time, maxOutstanding: 10, maxPerNetwork: 1, maxPerChannelClient: 2, maxChannelNoncesPerNetwork: 2);
        shared.IssueForNetwork(networkA);

        bool[] refused = [.. Enumerable.Range(0, 3).Select(_ => shared.IssueForChannel(ChannelIds.One, requesterA, networkA) is null)];

        Assert.Equal([false, false, true], refused);
    }

    [Fact]
    public void ShouldKeepChannelSharesSeparate() {
        NonceStore shared = new(time, maxOutstanding: 10, maxPerChannelClient: 1);
        shared.IssueForChannel(ChannelIds.One, requesterA, networkA);

        RelayNonce? other = shared.IssueForChannel(ChannelIds.Two, requesterA, networkA);

        Assert.NotNull(other);
    }

    [Fact]
    public void ShouldKeepChannelShareOfOtherRequesterWhenOneRequesterFillsIt() {
        NonceStore shared = new(time, maxOutstanding: 10, maxPerChannelClient: 1);
        shared.IssueForChannel(ChannelIds.One, requesterA, networkA);

        (bool, bool) outcomes = (
            shared.IssueForChannel(ChannelIds.One, requesterA, networkA) is null,
            shared.IssueForChannel(ChannelIds.One, requesterB, networkA) is null);

        Assert.Equal((true, false), outcomes);
    }

    [Fact]
    public void ShouldRefuseRequesterPastItsChannelNonceLimit() {
        NonceStore shared = new(time, maxOutstanding: 10, maxChannelNoncesPerClient: 2);
        shared.IssueForChannel(ChannelIds.One, requesterA, networkA);
        shared.IssueForChannel(ChannelIds.Two, requesterA, networkA);

        (bool, bool) outcomes = (
            shared.IssueForChannel(ChannelIds.Three, requesterA, networkA) is null,
            shared.IssueForChannel(ChannelIds.Three, requesterB, networkA) is null);

        Assert.Equal((true, false), outcomes);
    }

    [Fact]
    public void ShouldEvictOldestChannelNonceOfFullNetwork() {
        NonceStore shared = new(time, maxOutstanding: 10, maxChannelNoncesPerNetwork: 2);
        RelayNonce otherNetwork = shared.IssueForChannel(ChannelIds.One, otherNetworkRequester, networkB)!;
        RelayNonce oldest = shared.IssueForChannel(ChannelIds.One, requesterA, networkA)!;
        RelayNonce newer = shared.IssueForChannel(ChannelIds.Two, requesterB, networkA)!;
        shared.IssueForChannel(ChannelIds.Three, requesterB, networkA);

        (NonceOutcome, NonceOutcome, NonceOutcome) outcomes = (
            shared.Consume(oldest.Value), shared.Consume(newer.Value), shared.Consume(otherNetwork.Value));

        Assert.Equal((NonceOutcome.Expired, NonceOutcome.Valid, NonceOutcome.Valid), outcomes);
    }

    [Fact]
    public void ShouldEvictOldestOutstandingNonceWhenOlderOneWasConsumed() {
        NonceStore shared = new(time, maxOutstanding: 10, maxChannelNoncesPerNetwork: 2);
        RelayNonce consumed = shared.IssueForChannel(ChannelIds.One, requesterA, networkA)!;
        RelayNonce oldest = shared.IssueForChannel(ChannelIds.Two, requesterA, networkA)!;
        shared.Consume(consumed.Value);
        shared.IssueForChannel(ChannelIds.One, requesterB, networkA);
        shared.IssueForChannel(ChannelIds.One, requesterC, networkA);

        NonceOutcome outcome = shared.Consume(oldest.Value);

        Assert.Equal(NonceOutcome.Expired, outcome);
    }

    [Fact]
    public void ShouldEvictWithinFullNetworkWhenRelayIsFull() {
        NonceStore shared = new(time, maxOutstanding: 2, maxChannelNoncesPerNetwork: 2);
        shared.IssueForChannel(ChannelIds.One, requesterA, networkA);
        shared.IssueForChannel(ChannelIds.Two, requesterA, networkA);

        RelayNonce? next = shared.IssueForChannel(ChannelIds.Three, requesterB, networkA);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldIssueAgainToRequesterWhoseNonceWasEvicted() {
        NonceStore shared = new(time, maxOutstanding: 10, maxPerChannelClient: 1, maxChannelNoncesPerNetwork: 1);
        shared.IssueForChannel(ChannelIds.One, requesterA, networkA);
        shared.IssueForChannel(ChannelIds.Two, requesterB, networkA);

        RelayNonce? again = shared.IssueForChannel(ChannelIds.One, requesterA, networkA);

        Assert.NotNull(again);
    }

    [Fact]
    public void ShouldKeepLoneNonceWhenNeighboursRotateThroughFullNetwork() {
        const int perClient = 16;
        const int perNetwork = 256;
        const int flooders = perNetwork / perClient;
        const int floodIssues = perNetwork * 2;
        NonceStore shared = new(time, maxChannelNoncesPerNetwork: perNetwork, maxChannelNoncesPerClient: perClient);
        RelayNonce lone = shared.IssueForChannel(ChannelIds.One, requesterA, networkA)!;
        for (int issue = 0; issue < floodIssues; issue++) {
            shared.IssueForChannel(new ChannelId($"flood-{issue}"), $"flooder-{issue % flooders}", networkA);
        }

        NonceOutcome outcome = shared.Consume(lone.Value);

        Assert.Equal(NonceOutcome.Valid, outcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldFreeNetworkChannelShareWhenNonceIsConsumedOrExpires(bool consumed) {
        NonceStore shared = new(time, maxOutstanding: 10, maxChannelNoncesPerNetwork: 2);
        RelayNonce first = shared.IssueForChannel(ChannelIds.One, requesterA, networkA)!;
        time.Advance(justBeforeExpiry);
        RelayNonce second = shared.IssueForChannel(ChannelIds.Two, requesterB, networkA)!;
        if (consumed) {
            shared.Consume(first.Value);
        } else {
            time.Advance(nonceLifetime - justBeforeExpiry);
        }

        shared.IssueForChannel(ChannelIds.Three, requesterA, networkA);

        NonceOutcome outcome = shared.Consume(second.Value);

        Assert.Equal(NonceOutcome.Valid, outcome);
    }

    private static RelayNonce Issued(NonceStore nonces) =>
        nonces.IssueForNetwork(networkA) ?? throw new InvalidOperationException("nonce store refused an issue below its limit");
}
