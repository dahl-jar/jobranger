using System.Collections;
using System.Collections.Frozen;
using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Jobranger.Cli.Platform;

namespace Jobranger.Cli.Secrets;

[UnsupportedOSPlatform("windows")]
internal sealed class Credentials(IDictionary environment, ISecretStore? store, FileSecretStore file) {
    public static readonly FrozenDictionary<string, string> EnvVars = new Dictionary<string, string> {
        ["anthropic_api_key"] = "ANTHROPIC_API_KEY",
        ["openai_api_key"] = "OPENAI_API_KEY",
        ["openrouter_api_key"] = "OPENROUTER_API_KEY",
        ["smtp_password"] = "SMTP_PASSWORD",
        ["resend_api_key"] = "RESEND_API_KEY",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private const char maskChar = '•';
    private const int maskLength = 12;
    private const int visibleTail = 4;

    public bool IsFileOnly => EnvironmentVariables.Read(environment, SecretStores.SelectVariable) == SecretStores.FileOnly;

    public static string Mask(string value) =>
        new string(maskChar, maskLength) + (value.Length >= KeyGuard.FragmentChars ? value[^visibleTail..] : string.Empty);

    public async Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken) {
        if (EnvVars.TryGetValue(name, out string? variable)
            && EnvironmentVariables.Read(environment, variable) is string fromEnv) {
            return fromEnv;
        }
        if (store is not null
            && await ReadFromAsync(SecretLocation.Store, name, cancellationToken).ConfigureAwait(false) is string stored) {
            return stored;
        }
        return await ReadFromAsync(SecretLocation.File, name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ReadFromAsync(SecretLocation location, string name, CancellationToken cancellationToken) {
        if (location == SecretLocation.File) {
            return file.Read(name);
        }
        return store is null
            ? throw new SecretStoreMissingException(name)
            : await store.ReadAsync(name, cancellationToken).ConfigureAwait(false);
    }

    public FileLock Lock(CancellationToken cancellationToken) => file.Lock(cancellationToken);

    public async Task<SecretLocation> SetSecretAsync(string name, string value, CancellationToken cancellationToken) {
        using FileLock held = file.Lock(cancellationToken);
        IReadOnlyDictionary<string, string> saved = file.Load();
        if (store is null) {
            file.Save(new Dictionary<string, string>(saved) { [name] = value });
            return SecretLocation.File;
        }
        await store.WriteAsync(name, value, cancellationToken).ConfigureAwait(false);
        RemoveFromFile(saved, name);
        return SecretLocation.Store;
    }

    public async Task DeleteSecretAsync(string name, CancellationToken cancellationToken) {
        using FileLock held = file.Lock(cancellationToken);
        IReadOnlyDictionary<string, string> saved = file.Load();
        if (store is not null) {
            await store.DeleteAsync(name, cancellationToken).ConfigureAwait(false);
        }
        RemoveFromFile(saved, name);
    }

    private void RemoveFromFile(IReadOnlyDictionary<string, string> saved, string name) {
        if (!saved.ContainsKey(name)) {
            return;
        }
        file.Save(saved.Where(entry => entry.Key != name).ToDictionary(StringComparer.Ordinal));
    }
}
