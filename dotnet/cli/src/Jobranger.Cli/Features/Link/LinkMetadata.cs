using System.Runtime.Versioning;
using Jobranger.Cli.Files;
using Jobranger.Cli.Secrets;

namespace Jobranger.Cli.Features.Link;

[UnsupportedOSPlatform("windows")]
internal sealed record LinkMetadata(SecretLocation Store, string Channel, string Agreement) {
    public const string FileName = "link.json";

    private const string storeField = "store";
    private const string channelField = "channel";
    private const string agreementField = "agreement";
    private const string keyringLabel = "keyring";
    private const string fileLabel = "file";

    public static string StoreName(SecretLocation store) => store == SecretLocation.Store ? keyringLabel : fileLabel;

    public static LinkMetadata For(SecretLocation store, LinkKeyPair keys) =>
        new(store, keys.Channel.Value, keys.AgreementPublicKey);

    public static LinkMetadata? Read(FileSecretStore file) {
        if (!File.Exists(file.Path)) {
            return null;
        }
        IReadOnlyDictionary<string, string> values = file.Load();
        SecretLocation? store = values.GetValueOrDefault(storeField) switch {
            keyringLabel => SecretLocation.Store,
            fileLabel => SecretLocation.File,
            _ => null,
        };
        if (store is null
            || values.GetValueOrDefault(channelField) is not string channel
            || values.GetValueOrDefault(agreementField) is not string agreement) {
            throw new LinkKeyException(
                "link metadata needs store keyring or file, a channel and an agreement key "
                + $"path={file.Path}, {LinkKeys.UnpairHint}");
        }
        return new LinkMetadata(store.Value, channel, agreement);
    }

    public static void Delete(FileSecretStore file) {
        try {
            File.Delete(file.Path);
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            throw new PrivateFileException(file.Path, error.Message);
        }
    }

    public void Write(FileSecretStore file) => file.Save(new Dictionary<string, string> {
        [storeField] = StoreName(Store),
        [channelField] = Channel,
        [agreementField] = Agreement,
    });
}
