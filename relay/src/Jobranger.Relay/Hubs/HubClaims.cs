using System.Security.Claims;
using Jobranger.Contracts.Channels;
using Jobranger.Relay.Core;
using Microsoft.AspNetCore.SignalR;

namespace Jobranger.Relay.Hubs;

internal static class HubClaims {
    public const string ChannelType = "jr:ch";
    public const string RoleType = "jr:role";
    private const string grantType = "jr:grant";

    public static ClaimsPrincipal Principal(HandleClaims claims, string scheme) {
        ClaimsIdentity identity = new(
            [new Claim(ChannelType, claims.Channel.Value), new Claim(RoleType, RelayRoles.Wire(claims.Role))], scheme);
        if (claims.Grant is { } grant) {
            identity.AddClaim(new Claim(grantType, grant));
        }

        return new ClaimsPrincipal(identity);
    }

    public static HandleClaims? Claims(this ClaimsPrincipal? user) =>
        user?.FindFirstValue(ChannelType) is { } channel && RelayRoles.TryParse(user.FindFirstValue(RoleType), out RelayRole role)
            ? new HandleClaims(new ChannelId(channel), role, user.FindFirstValue(grantType))
            : null;

    public static HandleClaims Claims(this HubCallerContext caller) =>
        caller.User.Claims() ?? throw new InvalidOperationException("hub connection has no channel and role claims");
}
