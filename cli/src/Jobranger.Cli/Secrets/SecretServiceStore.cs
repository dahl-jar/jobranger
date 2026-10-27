using System.Collections;
using System.Security.Cryptography;
using System.Text;
using DBus.Services.Secrets;
using Jobranger.Cli.Platform;
using Tmds.DBus.Protocol;

namespace Jobranger.Cli.Secrets;

internal sealed class SecretServiceStore : ISecretStore {
    public const string BusVariable = "DBUS_SESSION_BUS_ADDRESS";

    private const string serviceUnknown = "org.freedesktop.DBus.Error.ServiceUnknown";
    private const string serviceAttribute = "service";
    private const string usernameAttribute = "username";
    private const string contentType = "text/plain";
    private const string defaultAlias = "default";
    private const string defaultLabel = "Default";
    private const string timeoutReason = "timeout";
    private const string noCollectionReason = "no default collection";
    private const string notCreatedReason = "item not created";
    private const string deleteDismissedReason = "delete dismissed";

    private static readonly TimeSpan callTimeout = TimeSpan.FromSeconds(30);

    private readonly SecretService secrets;
    private readonly string service;

    private SecretServiceStore(SecretService secrets, string service) {
        this.secrets = secrets;
        this.service = service;
    }

    public static Task<SecretServiceStore?> OpenAsync(
        IDictionary environment, string service, CancellationToken cancellationToken) =>
        OpenAsync(environment, service, () => SecretService.ConnectAsync(EncryptionType.Dh), callTimeout, cancellationToken);

    internal static async Task<SecretServiceStore?> OpenAsync(
        IDictionary environment,
        string service,
        Func<Task<SecretService>> connect,
        TimeSpan timeout,
        CancellationToken cancellationToken) {
        if (EnvironmentVariables.Read(environment, BusVariable) is null) {
            return null;
        }
        return await CallAsync<SecretServiceStore?>(service, async () => {
            try {
                return new SecretServiceStore(await connect(), service);
            } catch (DBusErrorReplyException error) when (error.ErrorName == serviceUnknown) {
                return null;
            } catch (DBusConnectionException) {
                return null;
            }
        }, timeout, cancellationToken).ConfigureAwait(false);
    }

    public Task<string?> ReadAsync(string name, CancellationToken cancellationToken) =>
        CallAsync(name, async () => {
            Item[] items = await (await DefaultCollectionAsync(name)).SearchItemsAsync(Attributes(name));
            return items.Length == 0 ? null : Encoding.UTF8.GetString(await items[0].GetSecretAsync());
        }, callTimeout, cancellationToken);

    public Task WriteAsync(string name, string value, CancellationToken cancellationToken) =>
        CallAsync(name, async () => {
            Collection collection = await DefaultCollectionAsync(name);
            Item? created = await collection.CreateItemAsync(
                $"Password for '{name}' on '{service}'", Attributes(name), Encoding.UTF8.GetBytes(value), contentType, true);
            if (created is null) {
                throw new SecretStoreUnavailableException(name, notCreatedReason);
            }
        }, callTimeout, cancellationToken);

    public Task DeleteAsync(string name, CancellationToken cancellationToken) =>
        CallAsync(name, async () => {
            Collection collection = await DefaultCollectionAsync(name);
            Item[] items = await collection.SearchItemsAsync(Attributes(name));
            foreach (Item item in items) {
                await item.DeleteAsync();
            }
            Item[] remaining = await collection.SearchItemsAsync(Attributes(name));
            RequireDeleted(name, remaining.Length);
        }, callTimeout, cancellationToken);

    internal static void RequireDeleted(string name, int remaining) {
        if (remaining > 0) {
            throw new SecretStoreUnavailableException(name, deleteDismissedReason);
        }
    }

    private async Task<Collection> DefaultCollectionAsync(string name) =>
        await secrets.GetDefaultCollectionAsync()
        ?? await secrets.CreateCollectionAsync(defaultLabel, defaultAlias)
        ?? throw new SecretStoreUnavailableException(name, noCollectionReason);

    private Dictionary<string, string> Attributes(string name) =>
        new() { [serviceAttribute] = service, [usernameAttribute] = name };

    internal static Task CallAsync(string name, Func<Task> call, TimeSpan timeout, CancellationToken cancellationToken) =>
        CallAsync(name, async () => {
            await call();
            return true;
        }, timeout, cancellationToken);

    internal static async Task<T> CallAsync<T>(
        string name, Func<Task<T>> call, TimeSpan timeout, CancellationToken cancellationToken) {
        try {
            return await call().WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        } catch (DBusErrorReplyException error) {
            throw new SecretStoreUnavailableException(name, error.ErrorName);
        } catch (Exception error) when (error is DBusConnectionException or CryptographicException) {
            throw new SecretStoreUnavailableException(name, error.GetType().Name);
        } catch (TimeoutException) {
            throw new SecretStoreUnavailableException(name, timeoutReason);
        }
    }
}
