using System.Runtime.Versioning;
using System.Text.Json;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Updates;

[UnsupportedOSPlatform("windows")]
internal static class VersionCheck {
    public const string ReleasesUrl = "https://api.github.com/repos/dahl-jar/jobranger/releases?per_page=100";

    private const string userAgent = "jobranger";
    private const string acceptType = "application/vnd.github+json";
    private const string tagPrefix = "cli-v";
    private const string tagField = "tag_name";
    private const string draftField = "draft";
    private const string prereleaseField = "prerelease";
    private const int maxBodyBytes = 8 * 1024 * 1024;

    private static readonly TimeSpan requestDeadline = TimeSpan.FromSeconds(2);

    public static async Task<Version?> LatestAsync(
        HttpClient client, string cachePath, DateTimeOffset now, CancellationToken cancellationToken) {
        if (VersionCache.TryRead(cachePath, now, out Version? cached)) {
            return cached;
        }
        Version? latest = await FetchAsync(client, cancellationToken).ConfigureAwait(false);
        VersionCache.Save(cachePath, now, latest);
        return latest;
    }

    private static async Task<Version?> FetchAsync(HttpClient client, CancellationToken cancellationToken) {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(requestDeadline);
        try {
            using HttpRequestMessage request = new(HttpMethod.Get, ReleasesUrl);
            request.Headers.UserAgent.ParseAdd(userAgent);
            request.Headers.Accept.ParseAdd(acceptType);
            using HttpResponseMessage response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            Stream body = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            await using (body.ConfigureAwait(false)) {
                return Highest(await CappedReader.ReadAsync(body, maxBodyBytes, deadline.Token).ConfigureAwait(false));
            }
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return null;
        } catch (Exception error) when (error is HttpRequestException or JsonException or IOException
            or InvalidOperationException or BodyTooLargeException) {
            return null;
        }
    }

    private static Version? Highest(byte[] body) {
        using JsonDocument releases = JsonDocument.Parse(body);
        if (releases.RootElement.ValueKind != JsonValueKind.Array) {
            return null;
        }
        Version? highest = null;
        foreach (JsonElement release in releases.RootElement.EnumerateArray()) {
            if (StableVersion(release) is Version version && (highest is null || version > highest)) {
                highest = version;
            }
        }
        return highest;
    }

    private static Version? StableVersion(JsonElement release) {
        if (release.ValueKind != JsonValueKind.Object
            || IsTrue(release, draftField)
            || IsTrue(release, prereleaseField)
            || !release.TryGetProperty(tagField, out JsonElement tag)
            || tag.ValueKind != JsonValueKind.String
            || tag.GetString() is not string name
            || !name.StartsWith(tagPrefix, StringComparison.Ordinal)) {
            return null;
        }
        return Version.TryParse(name[tagPrefix.Length..], out Version? version) ? version : null;
    }

    private static bool IsTrue(JsonElement release, string field) =>
        release.TryGetProperty(field, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
