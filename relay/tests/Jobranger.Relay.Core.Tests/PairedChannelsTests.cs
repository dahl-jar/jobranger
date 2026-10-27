using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests;

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
        paired.Record(Channels.One);

        (bool, bool) known = (paired.Touch(Channels.One), paired.Touch(Channels.Two));

        Assert.Equal((true, false), known);
    }

    [Fact]
    public void ShouldForgetChannelOnceTicketLifetimeEnds() {
        paired.Record(Channels.One);
        time.Advance(ticketLifetime);

        bool known = paired.Touch(Channels.One);

        Assert.False(known);
    }

    [Fact]
    public void ShouldExtendChannelWhenRecordedAgain() {
        paired.Record(Channels.One);
        time.Advance(sixtyDays);
        paired.Record(Channels.One);
        time.Advance(sixtyDays);

        bool known = paired.Touch(Channels.One);

        Assert.True(known);
    }

    [Fact]
    public void ShouldExtendChannelWhenTouched() {
        paired.Record(Channels.One);
        time.Advance(sixtyDays);
        paired.Touch(Channels.One);
        time.Advance(sixtyDays);

        bool known = paired.Touch(Channels.One);

        Assert.True(known);
    }

    [Fact]
    public void ShouldEvictLeastRecentlyUsedChannelWhenFull() {
        paired.Record(Channels.One);
        time.Advance(oneDay);
        paired.Record(Channels.Two);
        time.Advance(oneDay);
        paired.Touch(Channels.One);

        paired.Record(Channels.Three);

        Assert.Equal(
            (true, false, true),
            (paired.Touch(Channels.One), paired.Touch(Channels.Two), paired.Touch(Channels.Three)));
    }

    [Fact]
    public void ShouldDropExpiredChannelsOnSweep() {
        paired.Record(Channels.One);
        time.Advance(oneDay);
        paired.Record(Channels.Two);
        time.Advance(ticketLifetime - oneDay);
        paired.Sweep();

        int remaining = paired.Count;

        Assert.Equal(1, remaining);
    }

    [Fact]
    public void ShouldKeepEveryChannelWhenKnownChannelIsRecordedAgain() {
        paired.Record(Channels.One);
        paired.Record(Channels.Two);

        paired.Record(Channels.One);

        Assert.Equal((true, true), (paired.Touch(Channels.One), paired.Touch(Channels.Two)));
    }
}
