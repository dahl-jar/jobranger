using System.Buffers.Text;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Jobranger.Cli.Config;
using Jobranger.Cli.Http;
using Jobranger.Messaging.Channels;
using Jobranger.Messaging.Limits;
using Jobranger.Messaging.Proofs;
using NSec.Cryptography;

namespace Jobranger.Cli.Features.Link;

internal sealed partial class RelayApiClient(HttpClient client, Uri relay) {
    private const string nonceRoute = "pair/nonce";
    private const string startRoute = "pair/start";
    private const string sessionRoute = "session/cli";
    private const string versionField = "v";
    private const string channelField = "channel";
    private const string publicKeyField = "publicKey";
    private const string nonceField = "nonce";
    private const string signatureField = "signature";
    private const string codeField = "code";
    private const string expiresField = "expiresAt";
    private const string handleField = "handle";
    private const string bodyField = "body";
    private const string jsonType = "application/json";
    private const string unknownCode = "unknown";
    private const int maxBodyBytes = 64 * 1024;
    private const int clientErrorStart = 400;
    private const int serverErrorStart = 500;

    public async Task<PairingStart> StartPairingAsync(Key signing, CancellationToken cancellationToken) {
        using JsonDocument started =
            await ProveAsync(signing, startRoute, OwnershipProof.PairStartPurpose, cancellationToken).ConfigureAwait(false);
        string code = Text(started, codeField);
        if (!CodePattern().IsMatch(code)) {
            throw Invalid(codeField);
        }
        return new PairingStart(code, Time(started, expiresField), Text(started, handleField));
    }

    public async Task<string> CliHandleAsync(Key signing, CancellationToken cancellationToken) {
        using JsonDocument session =
            await ProveAsync(signing, sessionRoute, OwnershipProof.SessionCliPurpose, cancellationToken).ConfigureAwait(false);
        return Text(session, handleField);
    }

    private async Task<JsonDocument> ProveAsync(
        Key signing, string route, string purpose, CancellationToken cancellationToken) {
        byte[] publicKey = signing.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        string nonce = await NonceAsync(ChannelId.FromPublicKey(publicKey), cancellationToken).ConfigureAwait(false);
        byte[] signature = OwnershipProof.Sign(signing, purpose, nonce);
        return await PostAsync(route, writer => {
            writer.WriteNumber(versionField, MessageLimits.ContractVersion);
            writer.WriteString(publicKeyField, Base64Url.EncodeToString(publicKey));
            writer.WriteString(nonceField, nonce);
            writer.WriteString(signatureField, Base64Url.EncodeToString(signature));
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> NonceAsync(ChannelId channel, CancellationToken cancellationToken) {
        using JsonDocument issued = await PostAsync(nonceRoute, writer => {
            writer.WriteNumber(versionField, MessageLimits.ContractVersion);
            writer.WriteString(channelField, channel.Value);
        }, cancellationToken).ConfigureAwait(false);
        return Text(issued, nonceField);
    }

    private async Task<JsonDocument> PostAsync(
        string route, Action<Utf8JsonWriter> writeBody, CancellationToken cancellationToken) {
        using ByteArrayContent content = new(Body(writeBody));
        content.Headers.ContentType = new MediaTypeHeaderValue(jsonType);
        using HttpRequestMessage request = new(HttpMethod.Post, ServiceUrls.Join(relay, route)) {
            Content = content,
        };
        try {
            using HttpResponseMessage response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            byte[] body = await CappedReader.ReadAsync(stream, maxBodyBytes, cancellationToken).ConfigureAwait(false);
            int status = (int)response.StatusCode;
            if (status >= clientErrorStart && status < serverErrorStart) {
                throw new RelayRejectedException(status, ProblemCode(body));
            }
            if (!response.IsSuccessStatusCode) {
                throw new RelayUnavailableException(string.Create(
                    CultureInfo.InvariantCulture, $"relay failed route={route} status={status}"));
            }
            return Parse(body);
        } catch (Exception error) when (error is HttpRequestException or BodyTooLargeException
                                            || error is TaskCanceledException && !cancellationToken.IsCancellationRequested) {
            throw new RelayUnavailableException($"relay request failed route={route}", error);
        }
    }

    private static byte[] Body(Action<Utf8JsonWriter> writeBody) {
        using MemoryStream body = new();
        using (Utf8JsonWriter writer = new(body)) {
            writer.WriteStartObject();
            writeBody(writer);
            writer.WriteEndObject();
        }
        return body.ToArray();
    }

    private static JsonDocument Parse(byte[] body) {
        try {
            JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object) {
                return document;
            }
            document.Dispose();
        } catch (JsonException) {
            throw Invalid(bodyField);
        }
        throw Invalid(bodyField);
    }

    private static string ProblemCode(byte[] body) {
        try {
            using JsonDocument problem = JsonDocument.Parse(body);
            return problem.RootElement.ValueKind == JsonValueKind.Object
                && problem.RootElement.TryGetProperty(codeField, out JsonElement code)
                && code.ValueKind == JsonValueKind.String
                ? code.GetString()!
                : unknownCode;
        } catch (JsonException) {
            return unknownCode;
        }
    }

    private static string Text(JsonDocument document, string field) =>
        document.RootElement.TryGetProperty(field, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : throw Invalid(field);

    private static DateTimeOffset Time(JsonDocument document, string field) =>
        DateTimeOffset.TryParse(Text(document, field), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
            out DateTimeOffset time)
            ? time
            : throw Invalid(field);

    private static RelayUnavailableException Invalid(string field) => new($"relay answered with an invalid field={field}");

    [GeneratedRegex("^[BCDFGHJKLMNPQRSTVWXZ]{4}-[BCDFGHJKLMNPQRSTVWXZ]{4}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}
