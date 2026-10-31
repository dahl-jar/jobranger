using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Files;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Commands;

[UnsupportedOSPlatform("windows")]
internal static class LinkCommandRun {
    public static async Task<int> RunAsync(
        CliServices services,
        IDictionary environment,
        bool canPrompt,
        LinkOpener openLink,
        Action<ILogger, string> logFailure,
        LinkAction action,
        CancellationToken cancellationToken) {
        ILogger logger = services.Logs.CreateLogger(typeof(LinkCommandRun).FullName!);
        TextWriter output = services.Console.Profile.Out.Writer;
        IMessages messages = services.Messages;
        Paths paths = new(environment);
        try {
            ServiceUrls urls = ServiceUrls.From(environment);
            await using Link link =
                await openLink(environment, paths, urls, logger, canPrompt, cancellationToken).ConfigureAwait(false);
            return await action(new OpenedLink(link, urls, paths, output, logger), cancellationToken)
                .ConfigureAwait(false);
        } catch (Exception error) when (CommandFailure.Of(error, paths, messages) is ReadFailure failure) {
            logFailure(logger, failure.Code);
            foreach (string line in failure.Lines) {
                await output.WriteLineAsync(line).ConfigureAwait(false);
            }
            return CliExitCodes.Failure;
        }
    }

    public sealed record OpenedLink(Link Link, ServiceUrls Urls, Paths Paths, TextWriter Output, ILogger Logger);

    public delegate Task<int> LinkAction(OpenedLink opened, CancellationToken cancellationToken);

    public delegate Task<Link> LinkOpener(
        IDictionary environment,
        Paths paths,
        ServiceUrls urls,
        ILogger logger,
        bool canPrompt,
        CancellationToken cancellationToken);
}
