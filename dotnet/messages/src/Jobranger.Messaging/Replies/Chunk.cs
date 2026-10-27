using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Replies;

public sealed record Chunk(string ChunkId, int Index, int Count, string Data) : PayloadBase(Tag) {
    public const string Tag = "chunk";
}
