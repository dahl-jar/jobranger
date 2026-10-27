using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using Jobranger.Messaging.Fields;
using Jobranger.Messaging.Limits;

namespace Jobranger.Cli.Features.Sessions;

internal static class HelloSteps {
    private const string stepField = "t";
    private const string commitStep = "commit";
    private const string revealStep = "reveal";
    private const string confirmStep = "confirm";
    private const string abortStep = "abort";
    private const string offerStep = "offer";
    private const string unknownDeviceStep = "unknown_device";
    private const string unknownSessionStep = "unknown_session";
    private const string browserStaticField = "browser_static";
    private const string commitField = "commit";
    private const string pairTagField = "pair_tag";
    private const string browserEphField = "browser_eph";
    private const string nonceField = "nonce_b";
    private const string agentField = "agent";
    private const string macField = "mac";
    private const string cliStaticField = "cli_static";
    private const string cliEphField = "cli_eph";
    private const int commitBytes = 32;
    private const int macBytes = 32;
    private const int nonceBytes = 16;

    private static readonly Dictionary<string, string[]> requiredFields = new(StringComparer.Ordinal) {
        [commitStep] = [stepField, browserStaticField, commitField],
        [revealStep] = [stepField, browserEphField, nonceField, agentField],
        [confirmStep] = [stepField, macField],
        [abortStep] = [stepField],
    };

    private static readonly Dictionary<string, string[]> optionalFields = new(StringComparer.Ordinal) {
        [commitStep] = [pairTagField],
    };

    public static Step Parse(string ciphertext) {
        if (!CanonicalBase64Url.TryDecode(ciphertext, out byte[] utf8)
            || utf8.Length > MessageLimits.HelloMaxBytes) {
            throw Broken();
        }
        Dictionary<string, string> fields = ReadFields(utf8);
        string kind = fields.GetValueOrDefault(stepField) ?? throw Broken();
        if (!requiredFields.TryGetValue(kind, out string[]? required)
            || !HasExactly(fields, required, optionalFields.GetValueOrDefault(kind))) {
            throw Broken();
        }
        return kind switch {
            commitStep => new Commit(
                Binary(fields[browserStaticField], SessionSecrets.KeyBytes),
                Binary(fields[commitField], commitBytes),
                fields.TryGetValue(pairTagField, out string? tag) ? Binary(tag, MessageLimits.PairTagBytes) : null),
            revealStep => new Reveal(
                Binary(fields[browserEphField], SessionSecrets.KeyBytes),
                Binary(fields[nonceField], nonceBytes),
                Agent(fields[agentField])),
            confirmStep => new Confirm(Binary(fields[macField], macBytes)),
            abortStep => new Abort(),
            _ => throw Broken(),
        };
    }

    public static string Encode(Step step) {
        using MemoryStream content = new();
        using (Utf8JsonWriter writer = new(content)) {
            writer.WriteStartObject();
            switch (step) {
                case Offer offer:
                    writer.WriteString(stepField, offerStep);
                    writer.WriteString(cliStaticField, Base64Url.EncodeToString(offer.CliStatic));
                    writer.WriteString(cliEphField, Base64Url.EncodeToString(offer.CliEph));
                    break;
                case Confirm confirm:
                    writer.WriteString(stepField, confirmStep);
                    writer.WriteString(macField, Base64Url.EncodeToString(confirm.Mac));
                    break;
                case Abort:
                    writer.WriteString(stepField, abortStep);
                    break;
                case UnknownDevice:
                    writer.WriteString(stepField, unknownDeviceStep);
                    break;
                case UnknownSession:
                    writer.WriteString(stepField, unknownSessionStep);
                    break;
                default:
                    throw new ArgumentException($"the CLI never sends step type={step.GetType().Name}", nameof(step));
            }
            writer.WriteEndObject();
        }
        return Base64Url.EncodeToString(content.ToArray());
    }

    private static Dictionary<string, string> ReadFields(byte[] utf8) {
        Dictionary<string, string> fields = new(StringComparer.Ordinal);
        Utf8JsonReader reader = new(utf8);
        try {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) {
                throw Broken();
            }
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName) {
                string name = reader.GetString()!;
                if (!reader.Read()
                    || reader.TokenType != JsonTokenType.String
                    || !fields.TryAdd(name, reader.GetString()!)) {
                    throw Broken();
                }
            }
            if (reader.TokenType != JsonTokenType.EndObject || reader.Read()) {
                throw Broken();
            }
        } catch (Exception error) when (error is JsonException or InvalidOperationException) {
            throw Broken();
        }
        return fields;
    }

    private static bool HasExactly(Dictionary<string, string> fields, string[] required, string[]? optional) =>
        required.All(fields.ContainsKey)
        && fields.Keys.All(name => required.Contains(name) || (optional?.Contains(name) ?? false));

    private static byte[] Binary(string text, int byteLength) =>
        CanonicalBase64Url.TryDecode(text, byteLength, out byte[] bytes) ? bytes : throw Broken();

    private static string Agent(string agent) =>
        Encoding.UTF8.GetByteCount(agent) <= MessageLimits.AgentMaxBytes ? agent : throw Broken();

    private static HandshakeException Broken() => new(HandshakeFailure.Step);

    internal abstract record Step;

    internal sealed record Commit(byte[] BrowserStatic, byte[] Hash, byte[]? PairTag) : Step;

    internal sealed record Reveal(byte[] BrowserEph, byte[] NonceB, string Agent) : Step;

    internal sealed record Confirm(byte[] Mac) : Step;

    internal sealed record Offer(byte[] CliStatic, byte[] CliEph) : Step;

    internal sealed record Abort : Step;

    internal sealed record UnknownDevice : Step;

    internal sealed record UnknownSession : Step;
}
