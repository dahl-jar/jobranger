namespace Jobranger.Cli.Secrets;

internal sealed class SecretStoreUnavailableException(string name, string reason)
    : Exception($"secret store is present but failed name={name} error={reason}");
