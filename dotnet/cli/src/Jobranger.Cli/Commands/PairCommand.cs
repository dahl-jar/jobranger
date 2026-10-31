using System.Collections;
using System.CommandLine;
using System.Runtime.Versioning;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Features.Pairing;
using Jobranger.Cli.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal sealed class PairCommand : Command {
    public const string CommandName = "pair";

    public PairCommand(CliServices services)
        : this(
            services,
            Environment.GetEnvironmentVariables(),
            TimeProvider.System,
            Console.IsInputRedirected,
            Link.OpenAsync) {
    }

    public PairCommand(
        CliServices services,
        IDictionary environment,
        TimeProvider time,
        bool inputRedirected,
        LinkCommandRun.LinkOpener openLink)
        : base(CommandName, services.Messages.PairDescription) {
        SetAction((_, cancellationToken) => LinkCommandRun.RunAsync(
            services,
            environment,
            !inputRedirected,
            openLink,
            LogEvents.PairingFailed,
            (opened, token) => PairAsync(opened, services, time, token),
            cancellationToken));
    }

    private static Task<int> PairAsync(
        LinkCommandRun.OpenedLink opened, CliServices services, TimeProvider time, CancellationToken cancellationToken) {
        Pairing pairing = new(
            opened.Link, services.Messages, opened.Urls, opened.Paths, Environment.MachineName, time, opened.Logger);
        return pairing.RunAsync(services.Console, prompt => Ask(opened.Output, prompt), cancellationToken);
    }

    private static string Ask(TextWriter output, string prompt) {
        output.Write(prompt);
        output.Flush();
        return Console.In.ReadLine() ?? string.Empty;
    }
}
