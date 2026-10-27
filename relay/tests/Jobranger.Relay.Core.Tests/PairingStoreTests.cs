using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests;

public sealed class PairingStoreTests
{
    private static readonly TimeSpan justBeforeExpiry = new(0, 9, 59);
    private static readonly TimeSpan codeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan tombstoneEnd = TimeSpan.FromMinutes(20);
    private static readonly TimeSpan clockStepBack = TimeSpan.FromMinutes(1);
    private const int wrongGuessLimit = 5;
    private const int selectorCount = 160_000;
    private const int heavyTestTimeoutMs = 60_000;
    private const int entryLimit = 20_000;
    private const string network = "198.51.100.0/24";
    private const string otherNetwork = "203.0.113.0/24";

    private readonly FakeTimeProvider time = new();
    private readonly PairingStore store;

    public PairingStoreTests()
    {
        store = new PairingStore(time);
    }

    [Fact]
    public void ShouldClaimPendingPairingOnlyOnce()
    {
        PendingPairing pending = Started(store, Channels.One);
        store.Claim(pending.Code);

        ClaimResult second = store.Claim(pending.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.Expired, null), second);
    }

    [Fact]
    public void ShouldClaimPendingPairingBeforeTenMinutes()
    {
        PendingPairing pending = Started(store, Channels.One);
        time.Advance(justBeforeExpiry);

        ClaimResult result = store.Claim(pending.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.Claimed, Channels.One), result);
    }

    [Fact]
    public void ShouldExpirePendingPairingAfterTenMinutes()
    {
        PendingPairing pending = Started(store, Channels.One);
        time.Advance(codeLifetime);

        ClaimResult result = store.Claim(pending.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.Expired, null), result);
    }

    [Fact]
    public void ShouldAnswerWrongCodeForWrongVerifier()
    {
        PendingPairing pending = Started(store, Channels.One);

        ClaimResult result = store.Claim(WrongVerifier(pending.Code));

        Assert.Equal(new ClaimResult(ClaimOutcome.WrongCode, null), result);
    }

    [Fact]
    public void ShouldAnswerWrongCodeForUnknownSelector()
    {
        PendingPairing pending = Started(store, Channels.One);

        ClaimResult result = store.Claim(pending.Code with { Selector = Other(pending.Code.Selector) });

        Assert.Equal(new ClaimResult(ClaimOutcome.WrongCode, null), result);
    }

    [Fact]
    public void ShouldBurnPairingCodeAfterFiveWrongGuesses()
    {
        PendingPairing pending = Started(store, Channels.One);
        Guess(store, pending.Code, wrongGuessLimit);

        ClaimResult result = store.Claim(pending.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.Expired, null), result);
    }

    [Fact]
    public void ShouldClaimPairingAfterFourWrongGuesses()
    {
        PendingPairing pending = Started(store, Channels.One);
        Guess(store, pending.Code, wrongGuessLimit - 1);

        ClaimResult result = store.Claim(pending.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.Claimed, Channels.One), result);
    }

    [Fact]
    public void ShouldForgetOlderReplacedCodeOnSecondRestart()
    {
        PendingPairing first = Started(store, Channels.One);
        Started(store, Channels.One);
        Started(store, Channels.One);

        ClaimResult result = store.Claim(first.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.WrongCode, null), result);
    }

    [Fact]
    public void ShouldAnswerExpiredForCodeReplacedByNewerStart()
    {
        PendingPairing replaced = Started(store, Channels.One);
        store.Start(Channels.One, network);

        ClaimResult result = store.Claim(replaced.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.Expired, null), result);
    }

    [Fact]
    public void ShouldRefuseStartWhenPendingLimitIsReached()
    {
        PairingStore full = new(time, maxPending: 2);
        full.Start(Channels.One, network);
        full.Start(Channels.Two, network);

        PendingPairing? refused = full.Start(Channels.Three, network);

        Assert.Null(refused);
    }

    [Fact]
    public void ShouldRefuseNetworkAtItsPendingLimit()
    {
        PairingStore shared = new(time, maxPendingPerNetwork: 1);
        shared.Start(Channels.One, network);

        (bool, bool) refused = (shared.Start(Channels.Two, network) is null, shared.Start(Channels.Three, otherNetwork) is null);

        Assert.Equal((true, false), refused);
    }

    [Fact]
    public void ShouldFreeNetworkShareOnceItsCodeIsClaimed()
    {
        PairingStore shared = new(time, maxPendingPerNetwork: 1);
        shared.Claim(Started(shared, Channels.One).Code);

        PendingPairing? next = shared.Start(Channels.Two, network);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldFreeOneNetworkShareForEachClosedCode()
    {
        PairingStore shared = new(time, maxPendingPerNetwork: 2);
        PendingPairing first = Started(shared, Channels.One);
        shared.Start(Channels.Two, network);
        shared.Claim(first.Code);

        (bool, bool) refused = (shared.Start(Channels.Three, network) is null, shared.Start(new ChannelId("channel-four"), network) is null);

        Assert.Equal((false, true), refused);
    }

    [Fact]
    public void ShouldReplaceOwnPendingCodeAtNetworkLimit()
    {
        PairingStore shared = new(time, maxPendingPerNetwork: 1);
        shared.Start(Channels.One, network);

        PendingPairing? restarted = shared.Start(Channels.One, network);

        Assert.NotNull(restarted);
    }

    [Fact]
    public void ShouldAllowStartOncePendingPairingIsClaimed()
    {
        PairingStore full = new(time, maxPending: 1);
        PendingPairing pending = Started(full, Channels.One);
        full.Claim(pending.Code);

        PendingPairing? next = full.Start(Channels.Two, network);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldAllowStartOncePendingPairingIsBurned()
    {
        PairingStore full = new(time, maxPending: 1);
        PendingPairing pending = Started(full, Channels.One);
        Guess(full, pending.Code, wrongGuessLimit);

        PendingPairing? next = full.Start(Channels.Two, network);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldAllowStartOncePendingPairingExpires()
    {
        PairingStore full = new(time, maxPending: 1);
        full.Start(Channels.One, network);
        time.Advance(codeLifetime);

        PendingPairing? next = full.Start(Channels.Two, network);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldAllowStartWhenLaterCodeLapsesFirst()
    {
        PairingStore full = new(time, maxPending: 2);
        DateTimeOffset startedAt = time.GetUtcNow();
        full.Start(Channels.One, network);
        time.AdjustTime(startedAt - clockStepBack);
        full.Start(Channels.Two, network);
        time.AdjustTime(startedAt + justBeforeExpiry);

        PendingPairing? next = full.Start(Channels.Three, network);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldKeepCountingNewerCodeWhenReplacedCodeLapses()
    {
        PairingStore full = new(time, maxPending: 1);
        full.Start(Channels.One, network);
        time.Advance(justBeforeExpiry);
        full.Start(Channels.One, network);
        time.Advance(codeLifetime - justBeforeExpiry);

        PendingPairing? refused = full.Start(Channels.Two, network);

        Assert.Null(refused);
    }

    [Fact]
    public void ShouldAnswerExpiredForTombstoneBeforeRetentionPasses()
    {
        PendingPairing pending = Started(store, Channels.One);
        time.Advance(tombstoneEnd - TimeSpan.FromSeconds(1));

        store.Sweep();
        ClaimResult result = store.Claim(pending.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.Expired, null), result);
    }

    [Fact]
    public void ShouldForgetPairingOnceRetentionPasses()
    {
        PendingPairing pending = Started(store, Channels.One);
        time.Advance(TimeSpan.FromSeconds(1));
        Started(store, Channels.Two);
        time.Advance(tombstoneEnd - TimeSpan.FromSeconds(1));

        store.Sweep();
        ClaimResult result = store.Claim(pending.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.WrongCode, null), result);
    }

    [Fact]
    public void ShouldIssueCodeFromInjectedSource()
    {
        PairingStore seeded = new(time, drawCode: () => new PairingCode("KQFB", "MXTW"));

        PendingPairing? pending = seeded.Start(Channels.One, network);

        Assert.Equal(new PairingCode("KQFB", "MXTW"), pending?.Code);
    }

    [Fact]
    public void ShouldForgetEarlierPairingWhoseSelectorSortsAfterLaterOne()
    {
        Queue<PairingCode> codes = new([new PairingCode("ZZZZ", "BBBB"), new PairingCode("BBBB", "BBBB")]);
        PairingStore ordered = new(time, drawCode: codes.Dequeue);
        PendingPairing earlier = Started(ordered, Channels.One);
        time.Advance(TimeSpan.FromSeconds(1));
        Started(ordered, Channels.Two);
        time.Advance(tombstoneEnd - TimeSpan.FromSeconds(1));

        ordered.Sweep();
        ClaimResult result = ordered.Claim(earlier.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.WrongCode, null), result);
    }

    [Fact]
    public void ShouldForgetEveryPairingInOneSweep()
    {
        Started(store, Channels.One);
        time.Advance(TimeSpan.FromSeconds(1));
        PendingPairing last = Started(store, Channels.Two);
        time.Advance(tombstoneEnd);

        store.Sweep();
        ClaimResult result = store.Claim(last.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.WrongCode, null), result);
    }

    [Fact]
    public void ShouldForgetPairingsStartedAtSameTime()
    {
        Started(store, Channels.One);
        PendingPairing second = Started(store, Channels.Two);
        time.Advance(tombstoneEnd);

        store.Sweep();
        ClaimResult result = store.Claim(second.Code);

        Assert.Equal(new ClaimResult(ClaimOutcome.WrongCode, null), result);
    }

    [Fact]
    public void ShouldRefuseStartWhenEntryLimitHoldsOnlyPendingCodes()
    {
        PairingStore full = new(time, maxEntries: 2);
        Started(full, Channels.One);
        Started(full, Channels.Two);

        PendingPairing? refused = full.Start(Channels.Three, network);

        Assert.Null(refused);
    }

    [Fact]
    public void ShouldStartAfterEntryLimitOfClaimedCodes()
    {
        PairingStore busy = new(time);
        for (int pairing = 0; pairing < entryLimit; pairing++)
        {
            busy.Claim(Started(busy, new ChannelId($"channel-{pairing}")).Code);
        }

        PendingPairing? next = busy.Start(Channels.One, network);

        Assert.NotNull(next);
    }

    [Fact]
    public void ShouldEvictOldestTombstoneAtEntryLimit()
    {
        PairingStore full = new(time, maxEntries: 3);
        PendingPairing pending = Started(full, Channels.One);
        PendingPairing oldest = Started(full, Channels.Two);
        full.Claim(oldest.Code);
        time.Advance(TimeSpan.FromSeconds(1));
        PendingPairing newer = Started(full, Channels.Three);
        full.Claim(newer.Code);
        Started(full, new ChannelId("channel-four"));

        (ClaimOutcome, ClaimOutcome, ClaimOutcome) outcomes =
            (full.Claim(oldest.Code).Outcome, full.Claim(newer.Code).Outcome, full.Claim(pending.Code).Outcome);

        Assert.Equal((ClaimOutcome.WrongCode, ClaimOutcome.Expired, ClaimOutcome.Claimed), outcomes);
    }

    [Fact(Timeout = heavyTestTimeoutMs)]
    public async Task ShouldRefuseStartWhenNoFreeSelectorIsDrawn()
    {
        PairingStore crowded = new(time, maxPending: int.MaxValue, maxPendingPerNetwork: int.MaxValue, maxEntries: int.MaxValue);

        bool refused = await Task.Run(
            () => Enumerable.Range(0, selectorCount + 1).Any(index => crowded.Start(new ChannelId($"channel-{index}"), network) is null),
            TestContext.Current.CancellationToken);

        Assert.True(refused);
    }

    [Fact]
    public void ShouldReplaceOwnPendingCodeWhenPendingLimitIsReached()
    {
        PairingStore full = new(time, maxPending: 1);
        full.Start(Channels.One, network);

        PendingPairing? restarted = full.Start(Channels.One, network);

        Assert.NotNull(restarted);
    }

    [Fact]
    public void ShouldReplaceOwnPendingCodeWhenEntryLimitIsReached()
    {
        PairingStore full = new(time, maxEntries: 1);
        full.Start(Channels.One, network);

        PendingPairing? restarted = full.Start(Channels.One, network);

        Assert.NotNull(restarted);
    }

    [Fact]
    public void ShouldAllowOtherChannelAfterRepeatedRestarts()
    {
        const int entryLimit = 3;
        PairingStore full = new(time, maxEntries: entryLimit);
        for (int start = 0; start <= entryLimit; start++)
        {
            full.Start(Channels.One, network);
        }

        PendingPairing? other = full.Start(Channels.Two, network);

        Assert.NotNull(other);
    }

    [Fact]
    public void ShouldAnswerWrongCodeForMissingSelector()
    {
        PendingPairing pending = Started(store, Channels.One);

        ClaimResult result = store.Claim(pending.Code with { Selector = null! });

        Assert.Equal(new ClaimResult(ClaimOutcome.WrongCode, null), result);
    }

    [Fact]
    public void ShouldAnswerWrongCodeForMissingVerifier()
    {
        PendingPairing pending = Started(store, Channels.One);

        ClaimResult result = store.Claim(pending.Code with { Verifier = null! });

        Assert.Equal(new ClaimResult(ClaimOutcome.WrongCode, null), result);
    }

    private static PendingPairing Started(PairingStore target, ChannelId channel)
    {
        PendingPairing? pending = target.Start(channel, network);
        Assert.NotNull(pending);
        return pending;
    }

    private static void Guess(PairingStore target, PairingCode code, int times)
    {
        for (int guess = 0; guess < times; guess++)
        {
            target.Claim(WrongVerifier(code));
        }
    }

    private static PairingCode WrongVerifier(PairingCode code) => code with { Verifier = Other(code.Verifier) };

    private static string Other(string part) => part == "BBBB" ? "CCCC" : "BBBB";
}
