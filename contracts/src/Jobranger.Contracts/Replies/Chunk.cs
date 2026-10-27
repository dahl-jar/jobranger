using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Replies;

public sealed record Chunk(string ChunkId, int Index, int Count, string Data) : PayloadBase(Tag) {
    public const string Tag = "chunk";
}
