using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using Jobranger.Messaging.Envelopes;
using Jobranger.Messaging.Fields;
using Jobranger.Messaging.Limits;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Jobranger.Cli.Features.Link;

internal sealed class HubRelayConnection : IRelayConnection {
    private const string sendMethod = "Send";
    private const string receiveMethod = "Receive";
    private const string sessionsClosedMethod = "SessionsClosed";
    private const string reasonMarker = "HubException: ";
    private const string sidsField = "sids";
    private const int sidBytes = 16;

    private static readonly TimeSpan sendTimeout = TimeSpan.FromSeconds(30);

    private readonly HubConnection connection;
    private readonly SendPacer pacer;
    private readonly TimeProvider time;
    private readonly Channel<RelayEvent> events;

    public HubRelayConnection(HubConnection connection, Channel<RelayEvent> events, SendPacer pacer, TimeProvider time) {
        this.connection = connection;
        this.events = events;
        this.pacer = pacer;
        this.time = time;
        connection.On<JsonElement>(receiveMethod, OnReceive);
        connection.On<JsonElement>(sessionsClosedMethod, OnSessionsClosed);
        connection.Closed += _ => {
            events.Writer.TryWrite(new RelayEvent.Disconnected());
            return Task.CompletedTask;
        };
    }

    public Task StartAsync(CancellationToken cancellationToken) => connection.StartAsync(cancellationToken);

    public async Task SendAsync(Envelope envelope, CancellationToken cancellationToken) {
        if (envelope.Ciphertext.Length > MessageLimits.CiphertextB64MaxChars) {
            throw new FrameTooLargeException(envelope.Ciphertext.Length);
        }
        await pacer.WaitAsync(envelope.Ciphertext.Length, cancellationToken).ConfigureAwait(false);
        using CancellationTokenSource deadline = new(sendTimeout, time);
        using CancellationTokenSource sending =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try {
            await connection.InvokeAsync(sendMethod, envelope, sending.Token).ConfigureAwait(false);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (HubException refused)
            when (connection.State == HubConnectionState.Connected && Reason(refused.Message) is string reason) {
            throw new HubRefusedException(reason);
        } catch (Exception) {
            throw new RelayUnavailableException("relay hub send failed");
        }
    }

    public ValueTask<RelayEvent> ReceiveAsync(CancellationToken cancellationToken) =>
        events.Reader.ReadAsync(cancellationToken);

    public ValueTask DisposeAsync() => connection.DisposeAsync();

    private void OnReceive(JsonElement raw) {
        if (EnvelopeRules.TryParse(JsonMarshal.GetRawUtf8Value(raw), out Envelope? envelope)) {
            events.Writer.TryWrite(new RelayEvent.Received(envelope));
        }
    }

    private void OnSessionsClosed(JsonElement notice) {
        if (notice.ValueKind != JsonValueKind.Object
            || !notice.TryGetProperty(sidsField, out JsonElement sids)
            || sids.ValueKind != JsonValueKind.Array) {
            return;
        }
        List<string> closed = [
            .. sids.EnumerateArray()
                .Where(sid => sid.ValueKind == JsonValueKind.String)
                .Select(sid => sid.GetString()!)
                .Where(sid => CanonicalBase64Url.TryDecode(sid, sidBytes, out _)),
        ];
        if (closed.Count > 0) {
            events.Writer.TryWrite(new RelayEvent.SessionsClosed(closed));
        }
    }

    private static string? Reason(string message) {
        int marker = message.LastIndexOf(reasonMarker, StringComparison.Ordinal);
        return marker < 0 ? null : message[(marker + reasonMarker.Length)..];
    }
}
