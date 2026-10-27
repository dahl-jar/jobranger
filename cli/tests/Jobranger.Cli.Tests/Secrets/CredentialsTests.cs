using System.Collections;
using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Tests.Secrets;

[UnsupportedOSPlatform("windows")]
public sealed class CredentialsTests : IDisposable {
    private const string otherName = "openai_api_key";
    private const string storeKey = "sk-store-1234";
    private const string envKey = "sk-env-1234";
    private const string otherKey = "sk-other-1234";
    private const string configDirVariable = "JOBRANGER_CONFIG_DIR";

    private readonly TempDir temp = new();
    private readonly Hashtable environment = new();
    private readonly FileSecretStore file;

    public CredentialsTests() {
        Paths paths = new(new Hashtable { [configDirVariable] = temp.Path });
        file = new FileSecretStore(paths.SecretsPath(), NullLogger.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => temp.Dispose();

    [Fact]
    public async Task ShouldReturnEnvValue_WhenStoreAlsoHoldsSecret() {
        FakeSecretStore store = StoreHoldingKey();
        environment["ANTHROPIC_API_KEY"] = envKey;

        string? secret = await Build(store).GetSecretAsync(SecretFixtures.SecretName, Token);

        Assert.Equal(envKey, secret);
    }

    [Theory]
    [InlineData("anthropic_api_key", "ANTHROPIC_API_KEY")]
    [InlineData("openai_api_key", "OPENAI_API_KEY")]
    [InlineData("openrouter_api_key", "OPENROUTER_API_KEY")]
    [InlineData("smtp_password", "SMTP_PASSWORD")]
    [InlineData("resend_api_key", "RESEND_API_KEY")]
    public async Task ShouldReadSecretFromEnv_WhenEnvVarIsSet(string name, string variable) {
        environment[variable] = envKey;

        string? secret = await Build(new FakeSecretStore()).GetSecretAsync(name, Token);

        Assert.Equal(envKey, secret);
    }

    [Fact]
    public async Task ShouldReadFileValue_WhenNoStoreExists() {
        WriteFileKey();

        string? secret = await Build(null).GetSecretAsync(SecretFixtures.SecretName, Token);

        Assert.Equal(SecretFixtures.FileValue, secret);
    }

    [Fact]
    public async Task ShouldPreferStore_WhenFileAlsoHoldsSecret() {
        WriteFileKey();
        FakeSecretStore store = StoreHoldingKey();

        string? secret = await Build(store).GetSecretAsync(SecretFixtures.SecretName, Token);

        Assert.Equal(storeKey, secret);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("set")]
    [InlineData("delete")]
    public async Task ShouldThrowWithoutFileFallback_WhenStoreFails(string operation) {
        WriteFileKey();
        Credentials credentials = Build(new FakeSecretStore { IsBroken = true });

        Func<Task> act = operation switch {
            "get" => () => credentials.GetSecretAsync(SecretFixtures.SecretName, Token),
            "set" => () => credentials.SetSecretAsync(SecretFixtures.SecretName, storeKey, Token),
            _ => () => credentials.DeleteSecretAsync(SecretFixtures.SecretName, Token),
        };

        await Assert.ThrowsAsync<SecretStoreUnavailableException>(act);
        Assert.Equal(new Dictionary<string, string> { [SecretFixtures.SecretName] = SecretFixtures.FileValue }, file.Load());
    }

    [Fact]
    public async Task ShouldStoreInStore_WhenStoreExists() {
        FakeSecretStore store = new();

        SecretLocation location = await Build(store).SetSecretAsync(SecretFixtures.SecretName, storeKey, Token);

        Assert.Equal(SecretLocation.Store, location);
        Assert.Equal(storeKey, store.Values[SecretFixtures.SecretName]);
        Assert.False(File.Exists(file.Path));
    }

    [Fact]
    public async Task ShouldKeepOtherSecrets_WhenStoringInFile() {
        WriteFile(SecretFixtures.FileJson(otherName, otherKey));

        SecretLocation location = await Build(null).SetSecretAsync(SecretFixtures.SecretName, SecretFixtures.FileValue, Token);

        Assert.Equal(SecretLocation.File, location);
        Assert.Equal(
            new Dictionary<string, string> { [otherName] = otherKey, [SecretFixtures.SecretName] = SecretFixtures.FileValue },
            file.Load());
    }

    [Fact]
    public async Task ShouldDropFileCopy_WhenSecretMovesToStore() {
        WriteFile($"{{\"{SecretFixtures.SecretName}\": \"{SecretFixtures.FileValue}\", \"{otherName}\": \"{otherKey}\"}}");

        await Build(new FakeSecretStore()).SetSecretAsync(SecretFixtures.SecretName, storeKey, Token);

        Assert.Equal(new Dictionary<string, string> { [otherName] = otherKey }, file.Load());
    }

    [Fact]
    public async Task ShouldWriteNothingToStore_WhenSecretsFileIsDamaged() {
        WriteFile("{");
        FakeSecretStore store = new();

        await Assert.ThrowsAsync<PrivateFileException>(
            () => Build(store).SetSecretAsync(SecretFixtures.SecretName, storeKey, Token));

        Assert.Empty(store.Values);
    }

    [Fact]
    public async Task ShouldKeepStoreEntry_WhenDeleteMeetsDamagedFile() {
        WriteFile("{");
        FakeSecretStore store = StoreHoldingKey();

        await Assert.ThrowsAsync<PrivateFileException>(() => Build(store).DeleteSecretAsync(SecretFixtures.SecretName, Token));

        Assert.Equal(storeKey, store.Values[SecretFixtures.SecretName]);
    }

    [Fact]
    public async Task ShouldRemoveSecretEverywhere_WhenDeleting() {
        WriteFileKey();
        FakeSecretStore store = StoreHoldingKey();
        Credentials credentials = Build(store);

        await credentials.DeleteSecretAsync(SecretFixtures.SecretName, Token);

        Assert.Null(await credentials.GetSecretAsync(SecretFixtures.SecretName, Token));
    }

    [Fact]
    public async Task ShouldTightenConfigDir_WhenStoringSecret() {
        File.SetUnixFileMode(temp.Path, FileModes.SharedDir);

        await Build(new FakeSecretStore()).SetSecretAsync(SecretFixtures.SecretName, storeKey, Token);

        Assert.Equal(FileModes.OwnerOnlyDir, File.GetUnixFileMode(temp.Path));
    }

    [Fact]
    public async Task ShouldRemoveStaleTemps_WhenStoringSecret() {
        File.WriteAllText(temp.File($".{SecretFixtures.FileName}.abc123.tmp"), SecretFixtures.FileValue);
        File.WriteAllText(temp.File("notes.tmp"), "keep");

        await Build(null).SetSecretAsync(SecretFixtures.SecretName, SecretFixtures.FileValue, Token);

        string[] names = [.. Directory.EnumerateFileSystemEntries(temp.Path).Select(entry => Path.GetFileName(entry)).Order()];
        Assert.Equal(["notes.tmp", SecretFixtures.FileName, SecretFixtures.FileName + ".lock"], names);
    }

    [Fact]
    public void ShouldShowLastFourCharacters_WhenMaskingSecret() {
        string masked = Credentials.Mask("sk-0000-3f9a");

        Assert.Equal(new string('•', 12) + "3f9a", masked);
    }

    [Fact]
    public void ShouldHideWholeSecret_WhenShorterThanFragment() {
        string masked = Credentials.Mask("pass");

        Assert.Equal(new string('•', 12), masked);
    }

    private Credentials Build(ISecretStore? store) => new(environment, store, file);

    private static FakeSecretStore StoreHoldingKey() {
        FakeSecretStore store = new();
        store.Values[SecretFixtures.SecretName] = storeKey;
        return store;
    }

    private void WriteFileKey() =>
        WriteFile(SecretFixtures.FileJson(SecretFixtures.SecretName, SecretFixtures.FileValue));

    private void WriteFile(string content) {
        File.WriteAllText(file.Path, content);
        File.SetUnixFileMode(file.Path, FileModes.OwnerReadWrite);
    }
}
