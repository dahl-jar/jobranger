using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Tests.Mocks;

internal sealed class FakeSecretStore : ISecretStore {
    internal const string FailureReason = "fake failure";

    public Dictionary<string, string> Values { get; } = [];

    public bool IsBroken { get; init; }

    public Task<string?> ReadAsync(string name, CancellationToken cancellationToken) {
        ThrowIfBroken(name);
        return Task.FromResult(Values.GetValueOrDefault(name));
    }

    public Task WriteAsync(string name, string value, CancellationToken cancellationToken) {
        ThrowIfBroken(name);
        Values[name] = value;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken) {
        ThrowIfBroken(name);
        Values.Remove(name);
        return Task.CompletedTask;
    }

    private void ThrowIfBroken(string name) {
        if (IsBroken) {
            throw new SecretStoreUnavailableException(name, FailureReason);
        }
    }
}
