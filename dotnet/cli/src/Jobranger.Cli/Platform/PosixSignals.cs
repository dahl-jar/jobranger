using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Jobranger.Cli.Platform;

[UnsupportedOSPlatform("windows")]
internal sealed class PosixSignals : IDisposable {
    private readonly CancellationTokenSource source;
    private readonly PosixSignalRegistration interrupt;
    private readonly PosixSignalRegistration terminate;

    public PosixSignals(CancellationToken outer) {
        source = CancellationTokenSource.CreateLinkedTokenSource(outer);
        interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, Cancel);
        terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Cancel);
    }

    public CancellationToken Token => source.Token;

    public void Dispose() {
        interrupt.Dispose();
        terminate.Dispose();
        source.Dispose();
    }

    private void Cancel(PosixSignalContext context) {
        context.Cancel = true;
        _ = source.CancelAsync();
    }
}
