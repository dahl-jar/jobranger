using System.Collections;
using Jobranger.Cli.Files;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Text;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Setup;

internal sealed record SetupContext(
    IPrompter Prompter,
    TextWriter Output,
    IMessages Messages,
    Paths Paths,
    IDictionary Environment,
    TimeProvider Time,
    SetupSeams Seams,
    ILogger Logger) {
    public async Task<IReadOnlyList<string>> PickAtLeastOneAsync(
        string message, IReadOnlyList<PromptChoice> choices, CancellationToken cancellationToken) {
        while (true) {
            IReadOnlyList<string> picked = await Prompter.CheckboxAsync(message, choices, cancellationToken).ConfigureAwait(false);
            if (picked.Count > 0) {
                return picked;
            }
            await Output.WriteLineAsync(Messages.InitPickOne).ConfigureAwait(false);
        }
    }
}
