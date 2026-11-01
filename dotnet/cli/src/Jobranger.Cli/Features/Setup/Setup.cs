using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Features.Setup;

[UnsupportedOSPlatform("windows")]
internal sealed class Setup {
    private readonly SetupContext context;
    private readonly IReadOnlyList<ISetupStep> steps;
    private readonly SetupSecrets secrets;

    public Setup(SetupContext context, IReadOnlyList<ISetupStep> steps, SetupSecrets secrets) {
        this.context = context;
        this.steps = steps;
        this.secrets = secrets;
    }

    public static Setup Init(SetupContext context) {
        SetupSecrets secrets = new(context);
        return new Setup(
            context,
            [new ModelStep(context, secrets), new ProfileStep(context), new LimitsStep(context), new DeliveryStep(context, secrets)],
            secrets);
    }

    public async Task RunInitAsync(CancellationToken cancellationToken) {
        string path = context.Paths.ConfigPath();
        if (File.Exists(path) && !await ConfirmStartOverAsync(path, cancellationToken).ConfigureAwait(false)) {
            return;
        }
        RequireConfigDir();
        InitState state = await RunStepsAsync(cancellationToken).ConfigureAwait(false);
        ConfigFile.RequireValid(path, state.Config);
        SecretLocation? location = await SaveAsync(state, path).ConfigureAwait(false);
        await WriteSavedAsync(path, location).ConfigureAwait(false);
    }

    private Task<bool> ConfirmStartOverAsync(string path, CancellationToken cancellationToken) =>
        context.Prompter.ConfirmAsync(context.Messages.InitStartOver(path), false, cancellationToken);

    private void RequireConfigDir() {
        try {
            PrivateFile.RequireWritableDir(context.Paths.ConfigDir());
        } catch (PrivateDirException error) {
            throw new SetupSaveException(0, error);
        }
    }

    private async Task<InitState> RunStepsAsync(CancellationToken cancellationToken) {
        InitState state = InitState.Empty;
        for (int index = 0; index < steps.Count; index++) {
            await context.Output.WriteLineAsync().ConfigureAwait(false);
            await context.Output.WriteLineAsync(context.Messages.InitStepHeader(index + 1, steps.Count, steps[index].Title))
                .ConfigureAwait(false);
            state = await steps[index].RunAsync(state, cancellationToken).ConfigureAwait(false);
        }
        return state;
    }

    private async Task<SecretLocation?> SaveAsync(InitState state, string path) {
        SecretLocation? location = await secrets.SaveAsync(state.Secrets).ConfigureAwait(false);
        try {
            ConfigFile.SaveFresh(path, state.Config, context.Paths.Home(), context.Logger);
        } catch (Exception error) when (SetupSecrets.IsWriteFailure(error)) {
            throw new SetupSaveException(state.Secrets.Count, error);
        }
        return location;
    }

    private async Task WriteSavedAsync(string path, SecretLocation? location) {
        await context.Output.WriteLineAsync(context.Messages.InitSaved(path)).ConfigureAwait(false);
        if (location is SecretLocation stored) {
            await context.Output.WriteLineAsync(context.Messages.InitSecretsStored(Place(stored))).ConfigureAwait(false);
        }
        await context.Output.WriteLineAsync(context.Messages.InitNext).ConfigureAwait(false);
    }

    private string Place(SecretLocation location) {
        if (location == SecretLocation.File) {
            return context.Paths.SecretsPath();
        }
        return OperatingSystem.IsMacOS() ? context.Messages.InitStoreKeychain : context.Messages.InitStoreKeyring;
    }
}
