using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Time.Testing;

namespace Jobranger.Relay.Core.Tests;

public sealed class ChannelHandleTests
{
    private static readonly TimeSpan handleLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ticketLifetime = TimeSpan.FromDays(90);

    private readonly FakeTimeProvider time = new();
    private readonly byte[] key = RandomNumberGenerator.GetBytes(RelayLimits.MinHandleKeyBytes);
    private readonly ChannelHandle handles;

    public ChannelHandleTests()
    {
        handles = new ChannelHandle(new HandleKeys(key, null), time);
    }

    [Theory]
    [InlineData(RelayRole.Cli)]
    [InlineData(RelayRole.Web)]
    public void ShouldVerifyHandleItSigned(RelayRole role)
    {
        DateTimeOffset expires = time.GetUtcNow() + handleLifetime;
        string handle = handles.Sign(Channels.One, role, expires);

        HandleClaims? claims = handles.Verify(handle);

        Assert.Equal(new HandleClaims(Channels.One, role), claims);
    }

    [Fact]
    public void ShouldRejectHandleWithTamperedChannel()
    {
        string[] parts = handles.Sign(Channels.One, RelayRole.Cli, time.GetUtcNow() + handleLifetime).Split('.');
        string payload = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(parts[0]))
            .Replace(Channels.One.Value, Channels.Two.Value, StringComparison.Ordinal);
        string tampered = $"{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload))}.{parts[1]}";

        HandleClaims? claims = handles.Verify(tampered);

        Assert.Null(claims);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-dot")]
    [InlineData("!!.!!")]
    [InlineData("e30.!!")]
    public void ShouldRejectMalformedHandle(string? handle)
    {
        HandleClaims? claims = handles.Verify(handle);

        Assert.Null(claims);
    }

    public static IEnumerable<TheoryDataRow<Func<string, string>>> NonCanonicalVariants() =>
    [
        new(token => token.Insert(3, " ")) { TestDisplayName = "space inside body" },
        new(token => token + "=") { TestDisplayName = "padding on mac" },
        new(FlipTrailingBit) { TestDisplayName = "unused trailing bit on mac" },
    ];

    [Theory]
    [MemberData(nameof(NonCanonicalVariants))]
    public void ShouldRejectNonCanonicalHandle(Func<string, string> rewrite)
    {
        string handle = rewrite(handles.Sign(Channels.One, RelayRole.Cli, time.GetUtcNow() + handleLifetime));

        HandleClaims? claims = handles.Verify(handle);

        Assert.Null(claims);
    }

    [Fact]
    public void ShouldRejectHandleAtItsExpiry()
    {
        string handle = handles.Sign(Channels.One, RelayRole.Cli, time.GetUtcNow() + handleLifetime);
        time.Advance(handleLifetime);

        HandleClaims? claims = handles.Verify(handle);

        Assert.Null(claims);
    }

    [Fact]
    public void ShouldVerifyHandleSignedWithPreviousKey()
    {
        DateTimeOffset expires = time.GetUtcNow() + handleLifetime;
        string handle = handles.Sign(Channels.One, RelayRole.Cli, expires);
        ChannelHandle rotated = new(new HandleKeys(RandomNumberGenerator.GetBytes(RelayLimits.MinHandleKeyBytes), key), time);

        HandleClaims? claims = rotated.Verify(handle);

        Assert.Equal(new HandleClaims(Channels.One, RelayRole.Cli), claims);
    }

    [Fact]
    public void ShouldRejectSignedHandleWithUnknownRole()
    {
        long expires = (time.GetUtcNow() + handleLifetime).ToUnixTimeSeconds();
        byte[] body = Encoding.UTF8.GetBytes($$"""{"K":"h","C":"{{Channels.One.Value}}","R":"admin","E":{{expires}}}""");
        string handle = $"{Base64Url.EncodeToString(body)}.{Base64Url.EncodeToString(HMACSHA256.HashData(key, body))}";

        HandleClaims? claims = handles.Verify(handle);

        Assert.Null(claims);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"K\":\"t\",\"C\":\"x\",\"R\":\"web\",\"E\":\"soon\"}")]
    public void ShouldRejectSignedTicketWithMalformedPayload(string payload)
    {
        byte[] body = Encoding.UTF8.GetBytes(payload);
        string ticket = $"{Base64Url.EncodeToString(body)}.{Base64Url.EncodeToString(HMACSHA256.HashData(key, body))}";

        TicketCheck check = handles.VerifyTicket(ticket);

        Assert.Equal(new TicketCheck(TicketOutcome.Invalid, null), check);
    }

    [Fact]
    public void ShouldNotAcceptTicketAsHandle()
    {
        string ticket = handles.SignTicket(Channels.One, time.GetUtcNow() + ticketLifetime);

        HandleClaims? claims = handles.Verify(ticket);

        Assert.Null(claims);
    }

    [Fact]
    public void ShouldNotAcceptHandleAsTicket()
    {
        string handle = handles.Sign(Channels.One, RelayRole.Web, time.GetUtcNow() + handleLifetime);

        TicketCheck check = handles.VerifyTicket(handle);

        Assert.Equal(new TicketCheck(TicketOutcome.Invalid, null), check);
    }

    [Fact]
    public void ShouldAcceptTicketBeforeItsExpiry()
    {
        string ticket = handles.SignTicket(Channels.One, time.GetUtcNow() + ticketLifetime);
        time.Advance(ticketLifetime - TimeSpan.FromSeconds(1));

        TicketCheck check = handles.VerifyTicket(ticket);

        Assert.Equal(new TicketCheck(TicketOutcome.Valid, Channels.One), check);
    }

    [Fact]
    public void ShouldReportTicketExpiredAtItsExpiry()
    {
        string ticket = handles.SignTicket(Channels.One, time.GetUtcNow() + ticketLifetime);
        time.Advance(ticketLifetime);

        TicketCheck check = handles.VerifyTicket(ticket);

        Assert.Equal(new TicketCheck(TicketOutcome.Expired, null), check);
    }

    private static string FlipTrailingBit(string token)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        int last = alphabet.IndexOf(token[^1], StringComparison.Ordinal);
        return string.Concat(token.AsSpan(0, token.Length - 1), alphabet[last ^ 1].ToString());
    }
}
