using System.Globalization;
using System.Runtime.Versioning;
using System.Text;

namespace Jobranger.Cli.Secrets;

[SupportedOSPlatform("macos")]
internal sealed class KeychainSecretStore(
    string service, bool canPrompt, Func<bool, int> allowInteraction, Func<bool> isUnlocked) : ISecretStore {
    private const int success = 0;
    private const int itemNotFound = -25300;
    private const int interactionNotAllowed = -25308;
    private const int lockedWithoutPrompt = -25293;
    private const string statusReason = "status=";
    private const string approvalReason = "keychain is locked or needs approval in a terminal";

    public KeychainSecretStore(string service, bool canPrompt)
        : this(service, canPrompt, MacSecurity.SetUserInteractionAllowed, MacSecurity.IsDefaultKeychainUnlocked) {
    }

    public Task<string?> ReadAsync(string name, CancellationToken cancellationToken) {
        using MacSecurity.CoreFoundationObjects objects = new();
        nint query = Query(objects, name,
            (MacSecurity.ReturnDataKey, MacSecurity.BooleanTrue), (MacSecurity.MatchLimitKey, MacSecurity.MatchLimitOne));
        int status = MacSecurity.CopyMatching(query, out nint data);
        objects.Add(data);
        return Task.FromResult(ValueForStatus(name, status, () => Encoding.UTF8.GetString(MacSecurity.Bytes(data))));
    }

    internal static string? ValueForStatus(string name, int status, Func<string> value) {
        if (status == itemNotFound) {
            return null;
        }
        Check(name, status);
        return value();
    }

    public Task WriteAsync(string name, string value, CancellationToken cancellationToken) {
        Remove(name);
        using MacSecurity.CoreFoundationObjects objects = new();
        nint data = objects.Add(MacSecurity.CreateData(Encoding.UTF8.GetBytes(value)));
        Check(name, MacSecurity.AddItem(Query(objects, name, (MacSecurity.ValueDataKey, data)), nint.Zero));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken) {
        Remove(name);
        return Task.CompletedTask;
    }

    private void Remove(string name) {
        using MacSecurity.CoreFoundationObjects objects = new();
        int status = MacSecurity.DeleteItem(Query(objects, name));
        if (status != itemNotFound) {
            Check(name, status);
        }
    }

    internal void CheckAccess(string name) {
        if (canPrompt) {
            return;
        }
        Check(name, allowInteraction(false));
        if (!isUnlocked()) {
            throw new SecretStoreUnavailableException(name, approvalReason);
        }
    }

    private nint Query(MacSecurity.CoreFoundationObjects objects, string name, params (nint Key, nint Value)[] extra) {
        CheckAccess(name);
        nint[] keys = [MacSecurity.ClassKey, MacSecurity.ServiceKey, MacSecurity.AccountKey, .. extra.Select(pair => pair.Key)];
        nint[] values = [
            MacSecurity.GenericPassword,
            objects.Add(MacSecurity.CreateString(service)),
            objects.Add(MacSecurity.CreateString(name)),
            .. extra.Select(pair => pair.Value),
        ];
        return objects.Add(MacSecurity.CreateDictionary(
            nint.Zero, keys, values, keys.Length, MacSecurity.KeyCallbacks, MacSecurity.ValueCallbacks));
    }

    private static void Check(string name, int status) {
        if (status is interactionNotAllowed or lockedWithoutPrompt) {
            throw new SecretStoreUnavailableException(name, approvalReason);
        }
        if (status != success) {
            throw new SecretStoreUnavailableException(name, statusReason + status.ToString(CultureInfo.InvariantCulture));
        }
    }
}
