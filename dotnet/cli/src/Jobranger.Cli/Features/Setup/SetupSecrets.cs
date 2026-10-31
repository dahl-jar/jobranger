using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Features.Setup;

[UnsupportedOSPlatform("windows")]
internal sealed class SetupSecrets(SetupContext context) {
    private bool isOpen;
    private bool isReady;
    private ISecretStore? store;

    public static bool IsWriteFailure(Exception error) =>
        error is SecretStoreUnavailableException or PrivateFileException or IOException or UnauthorizedAccessException;

    public async Task<string> AskAsync(string name, string message, CancellationToken cancellationToken) {
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        string variable = Credentials.EnvVars[name];
        if (EnvironmentVariables.Read(context.Environment, variable) is not null) {
            await context.Output.WriteLineAsync(context.Messages.InitEnvVarWins(variable)).ConfigureAwait(false);
        }
        return await context.Prompter.SecretAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SecretLocation?> SaveAsync(IReadOnlyList<KeyValuePair<string, string>> secrets) {
        if (secrets.Count == 0) {
            return null;
        }
        SecretLocation? location = null;
        int written = 0;
        try {
            Credentials credentials = new(
                context.Environment,
                await OpenAsync(CancellationToken.None).ConfigureAwait(false),
                new FileSecretStore(context.Paths.SecretsPath(), context.Logger));
            foreach (KeyValuePair<string, string> secret in secrets) {
                location = await credentials.SetSecretAsync(secret.Key, secret.Value, CancellationToken.None)
                    .ConfigureAwait(false);
                written++;
            }
        } catch (Exception error) when (IsWriteFailure(error)) {
            throw new SetupSaveException(written, error);
        }
        return location;
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken) {
        if (isReady) {
            return;
        }
        if (await OpenAsync(cancellationToken).ConfigureAwait(false) is ISecretStore opened) {
            await opened.ReadAsync(Credentials.SmtpPasswordName, cancellationToken).ConfigureAwait(false);
        }
        isReady = true;
    }

    private async Task<ISecretStore?> OpenAsync(CancellationToken cancellationToken) {
        if (!isOpen) {
            store = await context.Seams.OpenSecrets(context.Environment, true, cancellationToken).ConfigureAwait(false);
            isOpen = true;
        }
        return store;
    }
}
