using Jobranger.Cli.Config;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Text;
using CatalogueEntry = Jobranger.Cli.Catalogue.Catalogue;
using ProfilesEntry = Jobranger.Cli.Features.Profiles.Profiles;

namespace Jobranger.Cli.Features.Setup;

internal sealed class ProfileStep(SetupContext context) : ISetupStep {
    private readonly IMessages messages = context.Messages;

    public string Title => messages.InitYouTitle;

    public async Task<InitState> RunAsync(InitState state, CancellationToken cancellationToken) {
        string path = await AskPathAsync(cancellationToken).ConfigureAwait(false);
        PromptChoice[] choices = [
            .. CatalogueEntry.Load().RoleList()
                .Select(role => new PromptChoice(role.Id, messages.InitRoleLabel(role.Id, role.Label))),
        ];
        IReadOnlyList<string> roles = await context.PickAtLeastOneAsync(messages.InitRoles, choices, cancellationToken)
            .ConfigureAwait(false);
        return state with {
            Config = state.Config with {
                Profile = new ProfileRef(path),
                Targets = state.Config.Targets with { Subfields = roles },
            },
        };
    }

    private async Task<string> AskPathAsync(CancellationToken cancellationToken) {
        string home = context.Paths.Home();
        string suggested = HomePath.Contract(context.Paths.ProfilePath(), home);
        while (true) {
            string answer = await context.Prompter.TextAsync(messages.InitProfilePath, suggested, cancellationToken)
                .ConfigureAwait(false);
            string path = Path.GetFullPath(HomePath.Expand(answer, home));
            try {
                ProfilesEntry.Load(path);
                return path;
            } catch (ConfigMissingException) {
                await context.Output.WriteLineAsync(messages.InitProfileMissing(path)).ConfigureAwait(false);
            } catch (ConfigInvalidException invalid) {
                foreach (ConfigError error in invalid.Errors) {
                    await context.Output.WriteLineAsync(error.Describe(messages)).ConfigureAwait(false);
                }
            }
        }
    }
}
