using System.Collections;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Secrets;

internal static class SecretStores {
    public const string ProductService = "jobranger";
    public const string SelectVariable = "JOBRANGER_SECRET_STORE";
    public const string FileOnly = "file";

    public static Task<ISecretStore?> OpenAsync(IDictionary environment, bool canPrompt, CancellationToken cancellationToken) =>
        OpenAsync(environment, token => ForPlatformAsync(environment, canPrompt, token), cancellationToken);

    public static async Task<ISecretStore?> OpenAsync(
        IDictionary environment,
        Func<CancellationToken, Task<ISecretStore?>> platformStore,
        CancellationToken cancellationToken) =>
        EnvironmentVariables.Read(environment, SelectVariable) == FileOnly
            ? null
            : await platformStore(cancellationToken).ConfigureAwait(false);

    private static async Task<ISecretStore?> ForPlatformAsync(
        IDictionary environment, bool canPrompt, CancellationToken cancellationToken) {
        if (OperatingSystem.IsMacOS()) {
            return new KeychainSecretStore(ProductService, canPrompt);
        }
        if (OperatingSystem.IsLinux()) {
            return await SecretServiceStore.OpenAsync(environment, ProductService, cancellationToken).ConfigureAwait(false);
        }
        return null;
    }
}
