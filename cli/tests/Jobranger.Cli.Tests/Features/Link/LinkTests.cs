using System.Buffers.Text;
using System.Collections;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Link;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Contracts.Proofs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using NSec.Cryptography;
using LinkEntry = Jobranger.Cli.Features.Link.Link;

namespace Jobranger.Cli.Tests.Features.Link;

[UnsupportedOSPlatform("windows")]
public sealed class LinkTests : IDisposable {
    private const string secretName = "link-keys";
    private const string metadataName = "link.json";
    private const string lockName = "link.json.lock";
    private const string vectorChannel = "QzATLaCZ1ciBwANAgYReA3Np4WJ48r7ZPzlZsVwdxSQ";
    private const string unpairHint = "run `jobranger unpair` to make new ones";
    private const string metadataRule = "link metadata needs store keyring or file, a channel and an agreement key";
    private const string pythonFixtures = "TestData/Link";
    private const int concurrentLoads = 8;
    private const UnixFileMode readOnlyDir = UnixFileMode.UserRead | UnixFileMode.UserExecute;

    private static readonly string vectorKey = Base64Url.EncodeToString(SHA256.HashData("jobranger vector cli ed25519"u8));
    private static readonly string vectorPair = Pair(vectorKey, vectorKey);
    private static readonly TimeSpan belowLockWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan shortWait = TimeSpan.FromMilliseconds(200);

    private readonly TempDir temp = new();
    private readonly FileSecretStore file;

    public LinkTests() {
        file = new FileSecretStore(temp.File(SecretFixtures.FileName), NullLogger.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() {
        File.SetUnixFileMode(temp.Path, FileModes.OwnerOnlyDir);
        temp.Dispose();
    }

    [Fact]
    public async Task ShouldDeriveSameChannelId_WhenKeysCameFromPythonCli() {
        foreach (string name in new[] { SecretFixtures.FileName, metadataName }) {
            File.Copy(Path.Combine(AppContext.BaseDirectory, pythonFixtures, name), temp.File(name));
        }
        string pythonChannel = ReadMetadata(Path.Combine(AppContext.BaseDirectory, pythonFixtures))["channel"]!.GetValue<string>();

        using LinkKeyPair loaded = await Build(null).LoadOrCreateKeysAsync(Token);

        Assert.Equal(pythonChannel, loaded.Channel.Value);
    }

    [Theory]
    [InlineData(true, "keyring")]
    [InlineData(false, "file")]
    public async Task ShouldRecordStoreChannelAndAgreement_WhenKeysAreCreated(bool hasStore, string storeName) {
        using LinkKeyPair created = await Build(hasStore ? new FakeSecretStore() : null).LoadOrCreateKeysAsync(Token);

        Assert.Equal(Metadata(storeName, created.Channel.Value, created.AgreementPublicKey), ReadMetadata(temp.Path).ToJsonString());
    }

    [Fact]
    public async Task ShouldLoadRotatedKeys_WhenKeysWereRotated() {
        FakeSecretStore store = new();
        using LinkKeyPair created = await Build(store).LoadOrCreateKeysAsync(Token);
        using LinkKeyPair rotated = await Build(store).RotateKeysAsync(Token);

        using LinkKeyPair loaded = await Build(store).LoadOrCreateKeysAsync(Token);

        Assert.NotEqual(PublicKeys(created), PublicKeys(rotated));
        Assert.Equal(PublicKeys(rotated), PublicKeys(loaded));
    }

    [Theory]
    [InlineData("store")]
    [InlineData("metadata")]
    public async Task ShouldLoadStoredPair_WhenRotateDiedAfterStoringIt(string failure) {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        store.AfterWrite = failure == "store"
            ? () => throw new SecretStoreUnavailableException(secretName, FakeSecretStore.FailureReason)
            : () => File.SetUnixFileMode(temp.Path, readOnlyDir);
        await Assert.ThrowsAnyAsync<Exception>(() => Build(store).RotateKeysAsync(Token));
        store.AfterWrite = null;
        File.SetUnixFileMode(temp.Path, FileModes.OwnerOnlyDir);
        using LinkKeyPair stored = LinkKeys.Parse(store.Values[secretName]);

        using LinkKeyPair loaded = await Build(store).LoadOrCreateKeysAsync(Token);

        Assert.Equal(PublicKeys(stored), PublicKeys(loaded));
    }

    [Fact]
    public async Task ShouldKeepMetadata_WhenRotateCannotRemoveIt() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        string recorded = File.ReadAllText(temp.File(metadataName));
        File.SetUnixFileMode(temp.Path, readOnlyDir);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(store).RotateKeysAsync(Token));

        Assert.Contains(metadataName, refused.Message, StringComparison.Ordinal);
        Assert.Equal(recorded, File.ReadAllText(temp.File(metadataName)));
    }

    [Theory]
    [InlineData("load")]
    [InlineData("rotate")]
    public async Task ShouldWriteKeysWithoutWaitingOnItself_WhenCredentialsTakeTheSecretsLock(string operation) {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(belowLockWait);
        LinkEntry link = Build(new FakeSecretStore());

        using LinkKeyPair written = operation == "load"
            ? await link.LoadOrCreateKeysAsync(deadline.Token)
            : await link.RotateKeysAsync(deadline.Token);

        Assert.Equal(written.Channel.Value, ReadMetadata(temp.Path)["channel"]!.GetValue<string>());
    }

    [Fact]
    public async Task ShouldRefuseNewKeys_WhenRecordedFileLostThePair() {
        using (await Build(null).LoadOrCreateKeysAsync(Token)) {
        }
        File.Delete(file.Path);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(null).LoadOrCreateKeysAsync(Token));

        Assert.StartsWith("link keys missing from recorded store store=file", refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(file.Path));
    }

    [Fact]
    public async Task ShouldRefuseFileCopy_WhenRecordedSecretStoreIsGone() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        await Credentials(null).SetSecretAsync(secretName, store.Values[secretName], Token);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(null).LoadOrCreateKeysAsync(Token));

        Assert.Contains("store=keyring", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldThrow_WhenAgreementKeyDiffersFromRecorded() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        store.Values[secretName] = Pair(StoredField(store, "signing"), vectorKey);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(store).LoadOrCreateKeysAsync(Token));

        Assert.Contains("agreement key, " + unpairHint, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldThrow_WhenPairDiffersFromRecordedChannel() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        store.Values[secretName] = vectorPair;

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(store).LoadOrCreateKeysAsync(Token));

        Assert.Contains("recorded channel, " + unpairHint, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldLoadRecordedPair_WhenFirstReadSeesRotateInProgress() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        string recordedChannel = ReadMetadata(temp.Path)["channel"]!.GetValue<string>();
        store.StaleReads.Enqueue(vectorPair);

        using LinkKeyPair loaded = await Build(store).LoadOrCreateKeysAsync(Token);

        Assert.Equal(recordedChannel, loaded.Channel.Value);
    }

    [Fact]
    public async Task ShouldKeepStoredPair_WhenRotateIsCanceledWaitingForLock() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        string recorded = store.Values[secretName];
        using FileLock? held = FileLock.TryAcquire(temp.File(lockName), NullLogger.Instance);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(shortWait);

        await Assert.ThrowsAsync<OperationCanceledException>(() => Build(store).RotateKeysAsync(deadline.Token));

        Assert.Equal(recorded, store.Values[secretName]);
    }

    [Fact]
    public async Task ShouldReleaseLinkLock_WhenSecretsLockFails() {
        string secretsLock = temp.File(SecretFixtures.FileName + ".lock");
        Directory.CreateDirectory(secretsLock);
        await Assert.ThrowsAsync<LinkKeyException>(() => Build(null).LoadOrCreateKeysAsync(Token));
        Directory.Delete(secretsLock);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(Token);
        deadline.CancelAfter(belowLockWait);

        using LinkKeyPair loaded = await Build(null).LoadOrCreateKeysAsync(deadline.Token);

        Assert.Equal(loaded.Channel.Value, ReadMetadata(temp.Path)["channel"]!.GetValue<string>());
    }

    [Fact]
    public void ShouldReleaseBothKeys_WhenPairIsDisposed() {
        LinkKeyPair pair = LinkKeys.Parse(vectorPair);
        using Key other = Key.Create(KeyAgreementAlgorithm.X25519);

        pair.Dispose();

        Assert.Throws<ObjectDisposedException>(() => OwnershipProof.Sign(pair.Signing, OwnershipProof.PairStartPurpose, "nonce"));
        Assert.Throws<ObjectDisposedException>(() => KeyAgreementAlgorithm.X25519.Agree(pair.Agreement, other.PublicKey));
    }

    [Theory]
    [MemberData(nameof(InvalidPairs))]
    public async Task ShouldThrowWithoutKeyMaterial_WhenStoredPairIsInvalid(string stored) {
        FakeSecretStore store = new();
        store.Values[secretName] = stored;

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(store).LoadOrCreateKeysAsync(Token));

        Assert.Contains("name=" + secretName, refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(vectorKey, refused.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, "keyring")]
    [InlineData(false, "file")]
    public async Task ShouldAdoptStoredPair_WhenMetadataIsMissing(bool inStore, string storeName) {
        FakeSecretStore store = new();
        await Credentials(inStore ? store : null).SetSecretAsync(secretName, vectorPair, Token);

        using LinkKeyPair loaded = await Build(store).LoadOrCreateKeysAsync(Token);

        Assert.Equal(Metadata(storeName, vectorChannel, loaded.AgreementPublicKey), ReadMetadata(temp.Path).ToJsonString());
    }

    [Fact]
    public async Task ShouldThrow_WhenStoreAndFileHoldDifferentPairs() {
        FakeSecretStore store = new();
        store.Values[secretName] = vectorPair;
        await Credentials(null).SetSecretAsync(secretName, Pair(vectorKey, Base64Url.EncodeToString(new byte[32])), Token);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(store).LoadOrCreateKeysAsync(Token));

        Assert.Contains("differ between secret store and file, " + unpairHint, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRefuseAdoption_WhenNoSecretStoreCanBeChecked() {
        await Credentials(null).SetSecretAsync(secretName, vectorPair, Token);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(null).LoadOrCreateKeysAsync(Token));

        Assert.Contains("cannot be checked store=keyring", refused.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(temp.File(metadataName)));
    }

    [Fact]
    public async Task ShouldAdoptFilePair_WhenFileStoreIsSelected() {
        await Credentials(null).SetSecretAsync(secretName, vectorPair, Token);

        using LinkKeyPair loaded = await Build(null, environment: FileOnly()).LoadOrCreateKeysAsync(Token);

        Assert.Equal(Metadata("file", vectorChannel, loaded.AgreementPublicKey), ReadMetadata(temp.Path).ToJsonString());
    }

    [Theory]
    [InlineData("{", "file cannot be used")]
    [InlineData("{}", metadataRule)]
    [InlineData("{\"store\": \"cloud\", \"channel\": \"c\", \"agreement\": \"a\"}", metadataRule)]
    [InlineData("{\"store\": \"keyring\", \"agreement\": \"a\"}", metadataRule)]
    [InlineData("{\"store\": \"keyring\", \"channel\": \"c\"}", metadataRule)]
    public async Task ShouldThrowNamingMetadataFile_WhenMetadataIsInvalid(string content, string rule) {
        File.WriteAllText(temp.File(metadataName), content);

        LinkKeyException refused =
            await Assert.ThrowsAsync<LinkKeyException>(() => Build(new FakeSecretStore()).LoadOrCreateKeysAsync(Token));

        Assert.StartsWith(rule, refused.Message, StringComparison.Ordinal);
        Assert.Contains(metadataName, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRemoveStaleTemps_WhenTakingLock() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        File.Delete(temp.File(metadataName));
        string[] stale = [temp.File(".secrets.json.crashed.tmp"), temp.File(".link.json.crashed.tmp")];
        foreach (string path in stale) {
            File.WriteAllText(path, vectorPair);
        }

        using LinkKeyPair loaded = await Build(store).LoadOrCreateKeysAsync(Token);

        Assert.DoesNotContain(stale, File.Exists);
    }

    [Fact]
    public async Task ShouldLeaveConfigDirMode_WhenKeysLoadWithoutLock() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        File.SetUnixFileMode(temp.Path, FileModes.SharedDir);

        using LinkKeyPair loaded = await Build(store).LoadOrCreateKeysAsync(Token);

        Assert.Equal(FileModes.SharedDir, File.GetUnixFileMode(temp.Path));
    }

    [Fact]
    public async Task ShouldThrowStoredKeyError_WhenLockFailsOnRetry() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        store.Values[secretName] = Pair(StoredField(store, "signing"), Base64Url.EncodeToString(new byte[31]));
        File.Delete(temp.File(lockName));
        File.SetUnixFileMode(temp.Path, readOnlyDir);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(store).LoadOrCreateKeysAsync(Token));

        Assert.Contains("field=agreement", refused.Message, StringComparison.Ordinal);
        Assert.IsType<PrivateFileException>(refused.InnerException);
    }

    [Theory]
    [InlineData("load")]
    [InlineData("rotate")]
    public async Task ShouldThrowLinkKeyError_WhenSecretsFileIsDamaged(string operation) {
        File.WriteAllText(file.Path, "{");
        LinkEntry link = Build(null);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => operation == "load"
            ? link.LoadOrCreateKeysAsync(Token)
            : link.RotateKeysAsync(Token));

        Assert.Contains(SecretFixtures.FileName, refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldThrowLinkKeyErrorNamingLock_WhenLockStaysBusy() {
        using FileLock? held = FileLock.TryAcquire(temp.File(lockName), NullLogger.Instance);

        LinkKeyException refused = await Assert.ThrowsAsync<LinkKeyException>(() => Build(new FakeSecretStore()).RotateKeysAsync(Token));

        Assert.StartsWith("link keys are busy", refused.Message, StringComparison.Ordinal);
        Assert.Contains("file=" + lockName, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("load", "link.keys.created")]
    [InlineData("rotate", "link.keys.rotated")]
    public async Task ShouldLogEventWithoutKeyMaterial_WhenKeysAreWritten(string operation, string eventName) {
        FakeSecretStore store = new();
        FakeLogger logger = new();
        LinkEntry link = Build(store, logger);

        using LinkKeyPair written = operation == "load"
            ? await link.LoadOrCreateKeysAsync(Token)
            : await link.RotateKeysAsync(Token);

        IReadOnlyList<FakeLogRecord> records = logger.Collector.GetSnapshot();
        Assert.Equal([eventName], records.Select(record => record.Id.Name));
        Assert.DoesNotContain(records, record => KeyFields(store).Any(key => record.Message.Contains(key, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ShouldNotLog_WhenKeysAreReused() {
        FakeSecretStore store = new();
        using (await Build(store).LoadOrCreateKeysAsync(Token)) {
        }
        FakeLogger logger = new();

        using LinkKeyPair loaded = await Build(store, logger).LoadOrCreateKeysAsync(Token);

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task ShouldAgreeOnOneKeyPair_WhenLoadsRunTogether() {
        Task<LinkKeyPair>[] loads = [
            .. Enumerable.Range(0, concurrentLoads).Select(_ => Task.Run(() => Build(null).LoadOrCreateKeysAsync(Token), Token)),
        ];

        LinkKeyPair[] loaded = await Task.WhenAll(loads);

        Assert.Single(loaded.Select(pair => pair.Channel.Value).Distinct());
        foreach (LinkKeyPair pair in loaded) {
            pair.Dispose();
        }
    }

    public static TheoryData<string> InvalidPairs() => [
        JsonValue.Create(vectorKey).ToJsonString(),
        "{" + vectorKey,
        new JsonObject { ["agreement"] = vectorKey }.ToJsonString(),
        new JsonObject { ["signing"] = vectorKey }.ToJsonString(),
        Pair(vectorKey, Base64Url.EncodeToString(new byte[31])),
        "{\"signing\": \"" + vectorKey + "\", \"agreement\": 1234}",
    ];

    private static string Pair(string signing, string agreement) =>
        new JsonObject { ["signing"] = signing, ["agreement"] = agreement }.ToJsonString();

    private static string Metadata(string store, string channel, string agreement) =>
        new JsonObject { ["store"] = store, ["channel"] = channel, ["agreement"] = agreement }.ToJsonString();

    private static JsonObject ReadMetadata(string dir) => JsonNode.Parse(File.ReadAllText(Path.Combine(dir, metadataName)))!.AsObject();

    private static string StoredField(FakeSecretStore store, string field) =>
        JsonNode.Parse(store.Values[secretName])![field]!.GetValue<string>();

    private static string[] KeyFields(FakeSecretStore store) => [StoredField(store, "signing"), StoredField(store, "agreement")];

    private static (string, string) PublicKeys(LinkKeyPair pair) => (pair.Channel.Value, pair.AgreementPublicKey);

    private static Hashtable FileOnly() => new() { [SecretStores.SelectVariable] = SecretStores.FileOnly };

    private Credentials Credentials(ISecretStore? store, IDictionary? environment = null) =>
        new(environment ?? new Hashtable(), store, file);

    private LinkEntry Build(ISecretStore? store, ILogger? logger = null, IDictionary? environment = null) =>
        new(Credentials(store, environment), temp.Path, logger ?? NullLogger.Instance);
}
