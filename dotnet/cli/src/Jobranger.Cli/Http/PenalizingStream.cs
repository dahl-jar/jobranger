namespace Jobranger.Cli.Http;

internal sealed class PenalizingStream(Stream inner, Action penalize) : ForwardingStream(inner) {
    public override bool CanWrite => false;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
        try {
            return await Inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        } catch (IOException) {
            penalize();
            throw;
        }
    }

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Flush() {
    }
}
