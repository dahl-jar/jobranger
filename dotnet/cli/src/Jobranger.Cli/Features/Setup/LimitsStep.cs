using Jobranger.Cli.Config;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Text;
using CatalogueEntry = Jobranger.Cli.Catalogue.Catalogue;

namespace Jobranger.Cli.Features.Setup;

internal sealed class LimitsStep(SetupContext context) : ISetupStep {
    private const char placeSeparator = ',';
    private const string placeJoin = ", ";
    private const string placeIndent = "  ";
    private const string finnLabel = "FINN";
    private const string linkedInLabel = "LinkedIn";

    private readonly IMessages messages = context.Messages;

    public string Title => messages.InitLimitsTitle;

    public async Task<InitState> RunAsync(InitState state, CancellationToken cancellationToken) {
        Limits defaults = new();
        IReadOnlyList<string> levels = await context.PickAtLeastOneAsync(
            messages.InitLevels,
            [
                .. Limits.AllLevels.Select(level =>
                    new PromptChoice(level, messages.InitLevelLabel(level), defaults.Levels.Contains(level))),
            ],
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> years = await context.PickAtLeastOneAsync(
            messages.InitYears,
            [
                .. Limits.AllYears.Select(band =>
                    new PromptChoice(band, messages.InitYearsLabel(band), defaults.Years.Contains(band))),
            ],
            cancellationToken).ConfigureAwait(false);
        bool hideMasters = await context.Prompter.ConfirmAsync(messages.InitHideMasters, true, cancellationToken)
            .ConfigureAwait(false);
        string[] places = await AskPlacesAsync(cancellationToken).ConfigureAwait(false);
        bool relocate = await AskRelocateAsync(places, cancellationToken).ConfigureAwait(false);
        SourcesConfig sources = await AskSourcesAsync(cancellationToken).ConfigureAwait(false);
        return state with {
            Config = state.Config with {
                Targets = state.Config.Targets with {
                    Limits = new Limits { Levels = levels, Years = years, HideMasters = hideMasters },
                    Places = places,
                    Relocate = relocate,
                },
                Sources = sources,
            },
        };
    }

    private async Task<string[]> AskPlacesAsync(CancellationToken cancellationToken) {
        string answer = await context.Prompter.TextAsync(messages.InitPlaces, null, cancellationToken).ConfigureAwait(false);
        return answer.Split(placeSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task<bool> AskRelocateAsync(string[] places, CancellationToken cancellationToken) {
        if (places.Length == 0) {
            await context.Output.WriteLineAsync(messages.InitAnywhere).ConfigureAwait(false);
            return true;
        }
        bool relocate = await context.Prompter.ConfirmAsync(messages.InitRelocate, true, cancellationToken).ConfigureAwait(false);
        string joined = string.Join(placeJoin, places);
        await context.Output.WriteLineAsync(relocate ? messages.InitPlacesMoving(joined) : placeIndent + joined)
            .ConfigureAwait(false);
        return relocate;
    }

    private async Task<SourcesConfig> AskSourcesAsync(CancellationToken cancellationToken) {
        string[] lines = [
            messages.InitSourcesHeading,
            messages.InitSourceNav,
            messages.InitSourceEmployers,
            messages.InitSourceFinn,
            messages.InitSourceLinkedIn,
            messages.InitSourceTerms,
            messages.InitSourcePersonal,
        ];
        foreach (string line in lines) {
            await context.Output.WriteLineAsync(line).ConfigureAwait(false);
        }
        bool optIn = await context.Prompter.ConfirmAsync(messages.InitOptIn, false, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> picked = optIn
            ? await context.Prompter.CheckboxAsync(
                messages.InitOptInWhich,
                [new PromptChoice(ConfigKeys.Finn, finnLabel), new PromptChoice(ConfigKeys.LinkedIn, linkedInLabel)],
                cancellationToken).ConfigureAwait(false)
            : [];
        return new SourcesConfig {
            Finn = picked.Contains(ConfigKeys.Finn),
            LinkedIn = picked.Contains(ConfigKeys.LinkedIn),
            EmployerFeeds = CatalogueEntry.Load().DefaultEmployerFeeds(),
        };
    }
}
