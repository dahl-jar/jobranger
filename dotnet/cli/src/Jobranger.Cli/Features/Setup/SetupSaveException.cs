namespace Jobranger.Cli.Features.Setup;

internal sealed class SetupSaveException(int secretsWritten, Exception cause) : Exception("init could not save", cause) {
    public int SecretsWritten { get; } = secretsWritten;
}
