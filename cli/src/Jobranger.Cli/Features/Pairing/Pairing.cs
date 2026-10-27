using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Files;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using SessionTable = Jobranger.Cli.Features.Sessions.Sessions;

namespace Jobranger.Cli.Features.Pairing;

[UnsupportedOSPlatform("windows")]
internal sealed class Pairing(
    Link.Link link, IMessages messages, ServiceUrls urls, Paths paths, string machine, TimeProvider time, ILogger logger) {
    private const string pairPage = "pair";
    private const string indent = "  ";
    private const double secondsPerMinute = 60;
    private const int fewestMinutes = 1;
    private const string keysFailure = "keys";
    private const string relayFailure = "relay_unreachable";

    public async Task<int> RunAsync(IAnsiConsole console, Func<string, string> ask, CancellationToken cancellationToken) {
        TextWriter output = console.Profile.Out.Writer;
        using FileLock? held = FileLock.TryAcquire(paths.PairLockPath(), logger);
        if (held is null) {
            await output.WriteLineAsync(messages.PairAlreadyRunning).ConfigureAwait(false);
            return CliExitCodes.Failure;
        }
        try {
            using LinkKeyPair keys = await link.LoadOrCreateKeysAsync(cancellationToken).ConfigureAwait(false);
            PairingStart start = await link.StartPairingAsync(keys, cancellationToken).ConfigureAwait(false);
            LogEvents.PairingStarted(logger);
            PrintCode(console, start);
            await link.ConnectAsync(start.Handle, cancellationToken).ConfigureAwait(false);
            using SessionTable sessions = new(keys.Agreement, keys.Channel, time);
            PairingRun run = new(link, sessions, messages, keys.Channel, start, machine, time, logger);
            return await run.RunAsync(output, ask, cancellationToken).ConfigureAwait(false);
        } catch (LinkKeyException) {
            return await FailAsync(output, messages.PairKeysBroken, keysFailure).ConfigureAwait(false);
        } catch (RelayRejectedException rejected) {
            LogEvents.PairingRefused(logger, rejected.Status, rejected.Code);
            await output.WriteLineAsync(messages.PairRelayRefused).ConfigureAwait(false);
            return CliExitCodes.Failure;
        } catch (RelayUnavailableException) {
            return await FailAsync(output, messages.PairRelayUnreachable, relayFailure).ConfigureAwait(false);
        }
    }

    private async Task<int> FailAsync(TextWriter output, string line, string failure) {
        LogEvents.PairingFailed(logger, failure);
        await output.WriteLineAsync(line).ConfigureAwait(false);
        return CliExitCodes.Failure;
    }

    private void PrintCode(IAnsiConsole console, PairingStart start) {
        string page = ServiceUrls.Join(urls.Site, pairPage).AbsoluteUri;
        int minutes = Math.Max(
            fewestMinutes, (int)Math.Ceiling((start.ExpiresAt - time.GetUtcNow()).TotalSeconds / secondsPerMinute));
        console.WriteLine(messages.PairOpenPage);
        console.MarkupLine($"{indent}[bold]{Markup.Escape(page)}[/]");
        console.WriteLine();
        console.WriteLine(messages.PairEnterCode);
        console.MarkupLine($"{indent}[bold]{Markup.Escape(start.Code)}[/]");
        console.WriteLine();
        console.MarkupLine($"[dim]{Markup.Escape(messages.PairCodeExpiresIn(minutes))}[/]");
    }
}
