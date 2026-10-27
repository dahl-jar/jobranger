using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Secrets;

internal static class LiveSecretStores {
    public const string LiveVariable = "JOBRANGER_SECRET_STORE_LIVE";
    public const string LockedVariable = "JOBRANGER_KEYCHAIN_LOCKED";
    public const string OffReason = "live secret store tests are off: set " + LiveVariable + "=1";
    public const string LockedOffReason = "locked keychain test is off: set " + LockedVariable + "=1 on macOS";
    public const string Collection = "secret service bus";
    public const string PythonService = "jobranger-ci-python";
    public const string PythonValue = "sk-fake-python-1234";

    private const string liveValue = "1";
    private const string testServicePrefix = "jobranger-tests-";
    private const string ciVariable = "GITHUB_ACTIONS";
    private const string ciValue = "true";

    public static bool IsLive =>
        Environment.GetEnvironmentVariable(LiveVariable) == liveValue
        && (!OperatingSystem.IsMacOS() || Environment.GetEnvironmentVariable(ciVariable) == ciValue);

    public static bool IsLockedKeychain =>
        IsLive && OperatingSystem.IsMacOS() && Environment.GetEnvironmentVariable(LockedVariable) == liveValue;

    public static string UniqueService() => testServicePrefix + Guid.NewGuid().ToString("N");

    public static async Task<ISecretStore> OpenAsync(string service, CancellationToken cancellationToken) {
        if (OperatingSystem.IsMacOS()) {
            return new KeychainSecretStore(service, canPrompt: false);
        }
        if (OperatingSystem.IsLinux()) {
            return await SecretServiceStore.OpenAsync(Environment.GetEnvironmentVariables(), service, cancellationToken)
                ?? throw new InvalidOperationException("no secret service on the session bus");
        }
        throw new PlatformNotSupportedException("live secret store tests run on macOS and Linux");
    }
}
