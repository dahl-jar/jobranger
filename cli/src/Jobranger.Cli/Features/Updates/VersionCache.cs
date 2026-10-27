using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Jobranger.Cli.Files;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobranger.Cli.Features.Updates;

[UnsupportedOSPlatform("windows")]
internal static class VersionCache {
    private const string checkedAtField = "checked_at";
    private const string latestField = "latest";
    private const string timestampFormat = "O";

    private static readonly TimeSpan checkInterval = TimeSpan.FromDays(1);

    public static bool TryRead(string path, DateTimeOffset now, out Version? latest) {
        latest = null;
        try {
            using FileStream? stream = PrivateFile.OpenRead(path, NullLogger.Instance);
            if (stream is null
                || JsonNode.Parse(stream) is not JsonObject entry
                || !TryParseEntry(entry, out DateTimeOffset checkedAt, out latest)) {
                return false;
            }
            TimeSpan age = now - checkedAt;
            return age >= TimeSpan.Zero && age < checkInterval;
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException
            or PrivateFileException or JsonException or InvalidOperationException) {
            return false;
        }
    }

    public static void Save(string path, DateTimeOffset now, Version? latest) {
        JsonObject entry = new() {
            [checkedAtField] = now.ToString(timestampFormat, CultureInfo.InvariantCulture),
            [latestField] = latest?.ToString(),
        };
        try {
            PrivateFile.Write(path, Encoding.UTF8.GetBytes(entry.ToJsonString()), NullLogger.Instance);
        } catch (Exception error) when (error is IOException or UnauthorizedAccessException or PrivateFileException) {
            return;
        }
    }

    private static bool TryParseEntry(JsonObject entry, out DateTimeOffset checkedAt, out Version? latest) {
        checkedAt = default;
        latest = null;
        return entry[checkedAtField] is JsonValue checkedValue
            && checkedValue.TryGetValue(out string? checkedText)
            && DateTimeOffset.TryParseExact(
                checkedText, timestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out checkedAt)
            && TryReadVersion(entry[latestField], out latest);
    }

    private static bool TryReadVersion(JsonNode? value, out Version? version) {
        version = null;
        if (value is null) {
            return true;
        }
        return value is JsonValue text && text.TryGetValue(out string? name) && Version.TryParse(name, out version);
    }
}
