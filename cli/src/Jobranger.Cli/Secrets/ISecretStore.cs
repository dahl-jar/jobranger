namespace Jobranger.Cli.Secrets;

internal interface ISecretStore {
    Task<string?> ReadAsync(string name, CancellationToken cancellationToken);

    Task WriteAsync(string name, string value, CancellationToken cancellationToken);

    Task DeleteAsync(string name, CancellationToken cancellationToken);
}
