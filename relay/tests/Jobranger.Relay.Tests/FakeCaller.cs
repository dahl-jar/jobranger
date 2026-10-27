using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;

namespace Jobranger.Relay.Tests;

internal sealed class FakeCaller(string connectionId) : HubCallerContext {
    public override string ConnectionId { get; } = connectionId;

    public override string? UserIdentifier => null;

    public override ClaimsPrincipal? User => null;

    public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

    public override IFeatureCollection Features { get; } = new FeatureCollection();

    public override CancellationToken ConnectionAborted => CancellationToken.None;

    public override void Abort() {
    }
}
