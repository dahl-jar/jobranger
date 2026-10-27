using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Learning;

internal sealed class VectorIndex(SqliteConnection connection) : IVectorIndex {
    private const string addSql =
        "INSERT INTO decision_vectors (decision_id, embedding) VALUES ($id, $embedding) "
        + "ON CONFLICT (decision_id) DO UPDATE SET embedding = excluded.embedding";
    private const string clearSql = "DELETE FROM decision_vectors";
    private const string allSql = "SELECT decision_id, embedding FROM decision_vectors";
    private const float identical = 1f;

    public void Add(long decisionId, float[] vector) => Add(null, decisionId, vector);

    internal void Add(SqliteTransaction? transaction, long decisionId, float[] vector) =>
        SqlCommands.Execute(connection, transaction, addSql,
            ("$id", decisionId), ("$embedding", MemoryMarshal.AsBytes(vector.AsSpan()).ToArray()));

    public void Clear() => SqlCommands.Execute(connection, null, clearSql);

    public IReadOnlyList<Neighbour> Nearest(float[] vector, int k) {
        if (!HasDirection(vector)) {
            return [];
        }
        using SqliteCommand command = SqlCommands.Create(connection, null, allSql);
        using SqliteDataReader reader = command.ExecuteReader();
        List<Neighbour> neighbours = [];
        while (reader.Read()) {
            byte[] blob = reader.GetFieldValue<byte[]>(1);
            ReadOnlySpan<float> stored = MemoryMarshal.Cast<byte, float>(blob);
            if (blob.Length != vector.Length * sizeof(float) || !HasDirection(stored)) {
                continue;
            }
            float similarity = TensorPrimitives.CosineSimilarity(vector, stored);
            neighbours.Add(new Neighbour(reader.GetInt64(0), identical - similarity));
        }
        return [.. neighbours.OrderBy(neighbour => neighbour.Distance).Take(k)];
    }

    private static bool HasDirection(ReadOnlySpan<float> vector) {
        float norm = TensorPrimitives.Norm(vector);
        return norm > 0f && float.IsFinite(norm);
    }
}
