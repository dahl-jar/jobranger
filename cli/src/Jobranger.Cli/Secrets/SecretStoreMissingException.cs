namespace Jobranger.Cli.Secrets;

internal sealed class SecretStoreMissingException(string name)
    : Exception($"secret store is missing name={name}");
