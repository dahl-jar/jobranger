using System.Text.Json;
using System.Threading.Channels;
using Jobranger.Relay.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Jobranger.Relay.Tests.Mocks;

internal sealed class HubPeer : IAsyncDisposable {
    public const string Receive = "Receive";
    public const string Presence = "Presence";
    public const string SessionsClosed = "SessionsClosed";
    public const string Replaced = "Replaced";

    private const string sendMethod = "Send";
    private const string reasonMarker = "HubException: ";
    private const string noReason = "";

    private static readonly TimeSpan wait = TimeSpan.FromSeconds(10);

    private readonly Dictionary<string, Channel<JsonElement>> inbox;
    private readonly Channel<bool> replaced = Channel.CreateUnbounded<bool>();
    private readonly TaskCompletionSource<string> closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public HubPeer(HubConnection connection) {
        Connection = connection;
        inbox = new[] { Receive, Presence, SessionsClosed }
            .ToDictionary(method => method, _ => Channel.CreateUnbounded<JsonElement>(), StringComparer.Ordinal);
        foreach ((string method, Channel<JsonElement> messages) in inbox) {
            connection.On<JsonElement>(method, message => messages.Writer.TryWrite(message.Clone()));
        }

        connection.On(Replaced, () => replaced.Writer.TryWrite(true));
        connection.Closed += error => {
            closed.TrySetResult(error is null ? noReason : Reason(error.Message));
            return Task.CompletedTask;
        };
    }

    public HubConnection Connection { get; }

    public Task<string> Closed => closed.Task.WaitAsync(wait, TestContext.Current.CancellationToken);

    private static string Reason(string message) {
        int marker = message.LastIndexOf(reasonMarker, StringComparison.Ordinal);
        return marker < 0 ? message : message[(marker + reasonMarker.Length)..];
    }

    public Task WasReplaced() => replaced.Reader
        .ReadAsync(TestContext.Current.CancellationToken)
        .AsTask()
        .WaitAsync(wait, TestContext.Current.CancellationToken);

    public async Task<string> Next(string method) {
        JsonElement message = await inbox[method].Reader
            .ReadAsync(TestContext.Current.CancellationToken)
            .AsTask()
            .WaitAsync(wait, TestContext.Current.CancellationToken);
        return Envelopes.Compact(message.GetRawText());
    }

    public Task Send(string envelope) {
        using JsonDocument document = JsonDocument.Parse(envelope);
        return Connection.InvokeAsync(sendMethod, document.RootElement.Clone(), TestContext.Current.CancellationToken);
    }

    public async Task<string> Rejection(string envelope) {
        HubException error = await Assert.ThrowsAsync<HubException>(() => Send(envelope));
        return Reason(error.Message);
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
