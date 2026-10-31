using Jobranger.Relay.Core.Pairing;
using Jobranger.Relay.Core.Tests.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests.Pairing;

public sealed class PairedChannelsTests {
    private const int capacity = 2;

    private static readonly TimeSpan ticketLifetime = TimeSpan.FromDays(90);
    private static readonly TimeSpan sixtyDays = TimeSpan.FromDays(60);
    private static readonly TimeSpan oneDay = TimeSpan.FromDays(1);

    private readonly FakeTimeProvider time = new();
    private readonly PairedChannels paired;

    public PairedChannelsTests() {
        paired = new PairedChannels(time, capacity);
    }

    [Fact]
    public void ShouldKnowOnlyRecordedChannels() {
        paired.Record(ChannelIds.One);

        (bool, bool) known = (paired.Touch(ChannelIds.One), paired.Touch(ChannelIds.Two));

        Assert.Equal((true, false), known);
    }

    [Fact]
    public void ShouldForgetChannelOnceTicketLifetimeEnds() {
        paired.Record(ChannelIds.One);
        time.Advance(ticketLifetime);

        bool known = paired.Touch(ChannelIds.One);

        Assert.False(known);
    }

    [Fact]
    public void ShouldExtendChannelWhenRecordedAgain() {
        paired.Record(ChannelIds.One);
        time.Advance(sixtyDays);
        paired.Record(ChannelIds.One);
        time.Advance(sixtyDays);

        bool known = paired.Touch(ChannelIds.One);

        Assert.True(known);
    }

    [Fact]
    public void ShouldExtendChannelWhenTouched() {
        paired.Record(ChannelIds.One);
        time.Advance(sixtyDays);
        paired.Touch(ChannelIds.One);
        time.Advance(sixtyDays);

        bool known = paired.Touch(ChannelIds.One);

        Assert.True(known);
    }

    [Fact]
    public void ShouldEvictLeastRecentlyUsedChannelWhenFull() {
        paired.Record(ChannelIds.One);
        time.Advance(oneDay);
        paired.Record(ChannelIds.Two);
        time.Advance(oneDay);
        paired.Touch(ChannelIds.One);

        paired.Record(ChannelIds.Three);

        Assert.Equal(
            (true, false, true),
            (paired.Touch(ChannelIds.One), paired.Touch(ChannelIds.Two), paired.Touch(ChannelIds.Three)));
    }

    [Fact]
    public void ShouldDropExpiredChannelsOnSweep() {
        paired.Record(ChannelIds.One);
        time.Advance(oneDay);
        paired.Record(ChannelIds.Two);
        time.Advance(ticketLifetime - oneDay);
        paired.Sweep();

        int remaining = paired.Count;

        Assert.Equal(1, remaining);
    }

    [Fact]
    public void ShouldKeepEveryChannelWhenKnownChannelIsRecordedAgain() {
        paired.Record(ChannelIds.One);
        paired.Record(ChannelIds.Two);

        paired.Record(ChannelIds.One);

        Assert.Equal((true, true), (paired.Touch(ChannelIds.One), paired.Touch(ChannelIds.Two)));
    }
}
