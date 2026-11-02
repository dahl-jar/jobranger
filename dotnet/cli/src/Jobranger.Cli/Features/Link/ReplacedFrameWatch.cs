using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Jobranger.Cli.Features.Link;

internal sealed class ReplacedFrameWatch(IHubProtocol inner, ChannelWriter<RelayEvent> events) : IHubProtocol {
    private const string replacedMethod = "Replaced";

    public string Name => inner.Name;

    public int Version => inner.Version;

    public TransferFormat TransferFormat => inner.TransferFormat;

    public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [NotNullWhen(true)] out HubMessage? message) {
        bool parsed = inner.TryParseMessage(ref input, binder, out message);
        if (message is InvocationMessage { Target: replacedMethod }) {
            events.TryWrite(new RelayEvent.Replaced());
        }
        return parsed;
    }

    public void WriteMessage(HubMessage message, IBufferWriter<byte> output) => inner.WriteMessage(message, output);

    public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message) => inner.GetMessageBytes(message);

    public bool IsVersionSupported(int version) => inner.IsVersionSupported(version);
}
