using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Jobranger.Contracts.Channels;
using Jobranger.Contracts.Fields;
using Jobranger.Relay.Core.Tickets;

namespace Jobranger.Relay.Core.Tokens;

public sealed class ChannelHandle(HandleKeys keys, TimeProvider time) {
    private const string handleKind = "h";
    private const string ticketKind = "t";
    private const int grantBytes = 16;

    public static string NewGrant() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(grantBytes));

    public string SignCli(ChannelId channel, DateTimeOffset expires) => SignHandle(channel, RelayRole.Cli, expires, null);

    public string SignWeb(ChannelId channel, DateTimeOffset expires, string grant) =>
        SignHandle(channel, RelayRole.Web, expires, grant);

    public string SignTicket(ChannelId channel, DateTimeOffset expires, string grant) =>
        Seal(new TokenPayload(ticketKind, channel.Value, RelayRoles.Wire(RelayRole.Web), expires.ToUnixTimeSeconds(), grant));

    public HandleClaims? Verify(string? handle) {
        if (Open(handle) is not { Kind: handleKind } payload
            || !RelayRoles.TryParse(payload.Role, out RelayRole role)
            || (role == RelayRole.Web && payload.Grant is null)
            || !IsLive(payload)) {
            return null;
        }

        return new HandleClaims(new ChannelId(payload.Channel), role, payload.Grant);
    }

    public TicketCheck VerifyTicket(string? ticket) {
        if (Open(ticket) is not { Kind: ticketKind, Grant: not null } payload) {
            return new TicketCheck(TicketOutcome.Invalid, null);
        }

        return IsLive(payload)
            ? new TicketCheck(TicketOutcome.Valid, new ChannelId(payload.Channel), payload.Grant)
            : new TicketCheck(TicketOutcome.Expired, null);
    }

    private string SignHandle(ChannelId channel, RelayRole role, DateTimeOffset expires, string? grant) =>
        Seal(new TokenPayload(handleKind, channel.Value, RelayRoles.Wire(role), expires.ToUnixTimeSeconds(), grant));

    private static bool Matches(byte[] key, byte[] body, byte[] mac) =>
        CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, body), mac);

    private string Seal(TokenPayload payload) {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, CoreJsonContext.Default.TokenPayload);
        return $"{Base64Url.EncodeToString(body)}.{Base64Url.EncodeToString(HMACSHA256.HashData(keys.Current, body))}";
    }

    private TokenPayload? Open(string? token) {
        int dot = token?.IndexOf('.', StringComparison.Ordinal) ?? -1;
        if (dot < 0) {
            return null;
        }

        ReadOnlySpan<char> bodyText = token.AsSpan(0, dot);
        ReadOnlySpan<char> macText = token.AsSpan(dot + 1);
        if (!CanonicalBase64Url.TryDecode(bodyText, out byte[] body)
            || !CanonicalBase64Url.TryDecode(macText, out byte[] mac)) {
            return null;
        }

        bool signed = Matches(keys.Current, body, mac) || (keys.Previous is { } previous && Matches(previous, body, mac));
        return signed ? Parse(body) : null;
    }

    private static TokenPayload? Parse(byte[] body) {
        try {
            return JsonSerializer.Deserialize(body, CoreJsonContext.Default.TokenPayload);
        } catch (JsonException) {
            return null;
        }
    }

    private bool IsLive(TokenPayload payload) => DateTimeOffset.FromUnixTimeSeconds(payload.Expires) > time.GetUtcNow();
}
