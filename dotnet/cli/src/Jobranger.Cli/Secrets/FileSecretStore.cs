using System.Runtime.Versioning;
using System.Text.Json;
using Jobranger.Cli.Files;
using Microsoft.Extensions.Logging;

namespace Jobranger.Cli.Secrets;

[UnsupportedOSPlatform("windows")]
internal sealed class FileSecretStore(string path, ILogger logger) {
    private const string lockSuffix = ".lock";
    private const string notJsonReason = "not UTF-8 JSON";
    private const string notStringsReason = "not a JSON object of strings";

    private static readonly TimeSpan lockWait = TimeSpan.FromSeconds(10);

    public string Path { get; } = path;

    public string? Read(string name) => Load().GetValueOrDefault(name);

    public FileLock Lock(CancellationToken cancellationToken) {
        FileLock held = Guard(() => FileLock.Acquire(Path + lockSuffix, lockWait, logger, cancellationToken));
        try {
            Guard(() => PrivateFile.RemoveStaleTemps(Path));
            return held;
        } catch {
            held.Dispose();
            throw;
        }
    }

    public IReadOnlyDictionary<string, string> Load() {
        using FileStream? stream = PrivateFile.OpenRead(Path, logger);
        if (stream is null) {
            return new Dictionary<string, string>();
        }
        byte[] content = Guard(() => ReadAll(stream));
        return Parse(content);
    }

    public void Save(IReadOnlyDictionary<string, string> values) {
        using MemoryStream content = new();
        using (Utf8JsonWriter writer = new(content)) {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, string> value in values) {
                writer.WriteString(value.Key, value.Value);
            }
            writer.WriteEndObject();
        }
        Guard(() => PrivateFile.Write(Path, content.ToArray(), logger));
    }

    private static byte[] ReadAll(FileStream stream) {
        using MemoryStream content = new();
        stream.CopyTo(content);
        return content.ToArray();
    }

    private Dictionary<string, string> Parse(byte[] content) {
        JsonDocument document;
        try {
            document = JsonDocument.Parse(content);
        } catch (JsonException) {
            throw Unreadable(notJsonReason);
        }
        using (document) {
            if (document.RootElement.ValueKind != JsonValueKind.Object) {
                throw Unreadable(notStringsReason);
            }
            try {
                return Strings(document.RootElement);
            } catch (InvalidOperationException) {
                throw Unreadable(notJsonReason);
            }
        }
    }

    private Dictionary<string, string> Strings(JsonElement root) {
        Dictionary<string, string> values = [];
        foreach (JsonProperty property in root.EnumerateObject()) {
            values[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()!
                : throw Unreadable(notStringsReason);
        }
        return values;
    }

    private PrivateFileException Unreadable(string reason) => new(Path, reason) { IsUnreadable = true };

    private T Guard<T>(Func<T> action) {
        try {
            return action();
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException) {
            throw new PrivateFileException(Path, error.Message);
        }
    }

    private void Guard(Action action) => Guard(() => {
        action();
        return true;
    });
}
