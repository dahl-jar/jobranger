namespace Jobranger.Messaging.Envelopes;

public sealed record Envelope(int V, string Channel, string Sid, long Seq, string Kind, string Ciphertext);
