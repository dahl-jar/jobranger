using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Storage;
using Jobranger.Messaging.Envelopes;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Features.Link;

[UnsupportedOSPlatform("windows")]
internal sealed partial class Link : IAsyncDisposable {
    private static readonly TimeSpan relayRequestTimeout = TimeSpan.FromSeconds(15);

    private readonly Credentials credentials;
    private readonly ILogger logger;
    private readonly FileSecretStore metadataFile;
    private readonly LinkParts parts;
    private readonly IDisposable[] owned;
    private IRelayConnection? connection;

    public Link(Credentials credentials, string configDir, ILogger logger, LinkParts parts)
        : this(credentials, configDir, logger, parts, []) {
    }

    private Link(Credentials credentials, string configDir, ILogger logger, LinkParts parts, IDisposable[] owned) {
        this.credentials = credentials;
        this.logger = logger;
        this.parts = parts;
        this.owned = owned;
        metadataFile = new FileSecretStore(Path.Combine(configDir, LinkMetadata.FileName), logger);
    }

    public static async Task<Link> OpenAsync(
        IDictionary environment,
        Paths paths,
        ServiceUrls urls,
        ILogger logger,
        bool canPrompt,
        CancellationToken cancellationToken) {
        ISecretStore? store = await SecretStores.OpenAsync(environment, canPrompt, cancellationToken).ConfigureAwait(false);
        Credentials credentials = new(environment, store, new FileSecretStore(paths.SecretsPath(), logger));
        SqliteConnection database = Database.Open(paths.DbPath(), logger);
        HttpClient http = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }) {
            Timeout = relayRequestTimeout,
        };
        LinkParts parts = new(
            new DeviceStore(database),
            new RelayApiClient(http, urls.Relay),
            new HubConnector(urls.Relay, TimeProvider.System));
        return new Link(credentials, paths.ConfigDir(), logger, parts, [http, database]);
    }

    public Task<LinkKeyPair> LoadOrCreateKeysAsync(CancellationToken cancellationToken) =>
        TranslateAsync(() => LoadOrCreateAsync(cancellationToken));

    public IReadOnlyList<Device> Devices() => parts.Devices.List();

    public string AddDevice(byte[] publicKey, string name, DateTimeOffset pairedAt) =>
        parts.Devices.Add(publicKey, name, pairedAt);

    public bool RemoveDevice(string id) => parts.Devices.Remove(id);

    public int RemoveAllDevices() => parts.Devices.RemoveAll();

    public Task<PairingStart> StartPairingAsync(LinkKeyPair keys, CancellationToken cancellationToken) =>
        parts.Api.StartPairingAsync(keys.Signing, cancellationToken);

    public async Task ConnectAsync(string handle, CancellationToken cancellationToken) {
        if (connection is not null) {
            throw new InvalidOperationException("link is already connected to the relay");
        }
        connection = await parts.Connector.ConnectAsync(handle, cancellationToken).ConfigureAwait(false);
    }

    public Task SendAsync(Envelope envelope, CancellationToken cancellationToken) =>
        Connected.SendAsync(envelope, cancellationToken);

    public ValueTask<RelayEvent> ReceiveAsync(CancellationToken cancellationToken) =>
        Connected.ReceiveAsync(cancellationToken);

    public async ValueTask DisposeAsync() {
        if (connection is not null) {
            await connection.DisposeAsync().ConfigureAwait(false);
            connection = null;
        }
        foreach (IDisposable resource in owned) {
            resource.Dispose();
        }
    }

    private IRelayConnection Connected =>
        connection ?? throw new InvalidOperationException("link is not connected to the relay");

    public Task<LinkKeyPair> RotateKeysAsync(CancellationToken cancellationToken) =>
        TranslateAsync(() => RotateAsync(cancellationToken));

    private static async Task<LinkKeyPair> TranslateAsync(Func<Task<LinkKeyPair>> operation) {
        try {
            return await operation().ConfigureAwait(false);
        } catch (PrivateFileException error) {
            throw new LinkKeyException(error.Message, error);
        } catch (TimeoutException error) {
            throw new LinkKeyException($"link keys are busy, {error.Message}", error);
        }
    }

    private async Task<LinkKeyPair> LoadOrCreateAsync(CancellationToken cancellationToken) {
        (LinkKeyPair? existing, LinkKeyException? failure) =
            await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null) {
            return existing;
        }
        using FileLock held = LockOrThrow(failure, cancellationToken);
        LinkKeyPair? locked = await LoadLockedAsync(cancellationToken).ConfigureAwait(false);
        if (locked is not null) {
            return locked;
        }
        if (parts.Devices.List().Count > 0) {
            throw new LinkKeyException($"paired devices exist but link keys are missing, {LinkKeys.UnpairHint}");
        }
        LinkKeyPair created = await RecordAsync(LinkKeys.NewPair(), cancellationToken).ConfigureAwait(false);
        LogCreated(logger);
        return created;
    }

    private async Task<LinkKeyPair> RotateAsync(CancellationToken cancellationToken) {
        using FileLock held = Lock(cancellationToken);
        LinkMetadata.Delete(metadataFile);
        LinkKeyPair rotated = await RecordAsync(LinkKeys.NewPair(), cancellationToken).ConfigureAwait(false);
        LogRotated(logger);
        return rotated;
    }

    private async Task<(LinkKeyPair? Existing, LinkKeyException? Failure)> LoadUnlockedAsync(
        CancellationToken cancellationToken) {
        try {
            LinkMetadata? metadata = LinkMetadata.Read(metadataFile);
            return metadata is null ? (null, null) : (await CheckAsync(metadata, cancellationToken).ConfigureAwait(false), null);
        } catch (LinkKeyException error) {
            return (null, error);
        }
    }

    private async Task<LinkKeyPair?> LoadLockedAsync(CancellationToken cancellationToken) {
        LinkMetadata? metadata = LinkMetadata.Read(metadataFile);
        return metadata is null
            ? await AdoptAsync(cancellationToken).ConfigureAwait(false)
            : await CheckAsync(metadata, cancellationToken).ConfigureAwait(false);
    }

    private FileLock LockOrThrow(LinkKeyException? failure, CancellationToken cancellationToken) {
        try {
            return Lock(cancellationToken);
        } catch (PrivateFileException error) when (failure is not null) {
            throw new LinkKeyException(failure.Message, error);
        }
    }

    private FileLock Lock(CancellationToken cancellationToken) {
        FileLock held = metadataFile.Lock(cancellationToken);
        try {
            credentials.Lock(cancellationToken).Dispose();
            return held;
        } catch {
            held.Dispose();
            throw;
        }
    }

    private async Task<LinkKeyPair?> AdoptAsync(CancellationToken cancellationToken) {
        string? filePair =
            await credentials.ReadFromAsync(SecretLocation.File, LinkKeys.SecretName, cancellationToken).ConfigureAwait(false);
        string? storePair;
        try {
            storePair = await credentials.ReadFromAsync(SecretLocation.Store, LinkKeys.SecretName, cancellationToken)
                .ConfigureAwait(false);
        } catch (SecretStoreMissingException error) {
            return AdoptWithoutStore(filePair, error);
        }
        if (storePair is not null && filePair is not null && storePair != filePair) {
            throw new LinkKeyException($"link keys differ between secret store and file, {LinkKeys.UnpairHint}");
        }
        (string? pair, SecretLocation location) = storePair is not null
            ? (storePair, SecretLocation.Store)
            : (filePair, SecretLocation.File);
        return pair is null ? null : Pin(location, pair);
    }

    private LinkKeyPair? AdoptWithoutStore(string? filePair, SecretStoreMissingException missing) {
        if (filePair is null) {
            return null;
        }
        return credentials.IsFileOnly
            ? Pin(SecretLocation.File, filePair)
            : throw new LinkKeyException(
                "secret store is missing so stored link keys cannot be checked "
                + $"store={LinkMetadata.StoreName(SecretLocation.Store)} name={LinkKeys.SecretName}, {LinkKeys.UnpairHint}",
                missing);
    }

    private async Task<LinkKeyPair> CheckAsync(LinkMetadata metadata, CancellationToken cancellationToken) {
        LinkKeyPair keys = LinkKeys.Parse(await ReadRecordedAsync(metadata.Store, cancellationToken).ConfigureAwait(false));
        try {
            if (keys.Channel.Value != metadata.Channel) {
                throw new LinkKeyException($"stored link keys do not match the recorded channel, {LinkKeys.UnpairHint}");
            }
            if (keys.AgreementPublicKey != metadata.Agreement) {
                throw new LinkKeyException(
                    $"stored link keys do not match the recorded agreement key, {LinkKeys.UnpairHint}");
            }
            return keys;
        } catch {
            keys.Dispose();
            throw;
        }
    }

    private async Task<string> ReadRecordedAsync(SecretLocation location, CancellationToken cancellationToken) {
        string recorded = $"store={LinkMetadata.StoreName(location)} name={LinkKeys.SecretName}";
        try {
            return await credentials.ReadFromAsync(location, LinkKeys.SecretName, cancellationToken).ConfigureAwait(false)
                ?? throw new LinkKeyException($"link keys missing from recorded store {recorded}");
        } catch (SecretStoreMissingException error) {
            throw new LinkKeyException($"secret store is missing {recorded}", error);
        }
    }

    private async Task<LinkKeyPair> RecordAsync(string pair, CancellationToken cancellationToken) =>
        Pin(await credentials.SetSecretAsync(LinkKeys.SecretName, pair, cancellationToken).ConfigureAwait(false), pair);

    private LinkKeyPair Pin(SecretLocation location, string pair) {
        LinkKeyPair keys = LinkKeys.Parse(pair);
        try {
            LinkMetadata.For(location, keys).Write(metadataFile);
            return keys;
        } catch {
            keys.Dispose();
            throw;
        }
    }

    [LoggerMessage(EventName = "link.keys.created", Level = LogLevel.Information, Message = "created link keys")]
    private static partial void LogCreated(ILogger logger);

    [LoggerMessage(EventName = "link.keys.rotated", Level = LogLevel.Information, Message = "rotated link keys")]
    private static partial void LogRotated(ILogger logger);
}
