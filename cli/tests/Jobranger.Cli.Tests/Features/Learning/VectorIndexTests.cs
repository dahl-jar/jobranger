using System.Runtime.Versioning;
using Jobranger.Cli.Features.Learning;
using Jobranger.Cli.Tests.Mocks;
using static Jobranger.Cli.Tests.Features.Learning.DecisionFixtures;

namespace Jobranger.Cli.Tests.Features.Learning;

[UnsupportedOSPlatform("windows")]
public sealed class VectorIndexTests : IDisposable {
    private const int dimension = 8;
    private const int vectorCount = 12;
    private const int k = 5;
    private const int seed = 1234;
    private const double tolerance = 1e-5;

    private readonly TestDatabase db = new();
    private readonly DecisionStore decisions;
    private readonly VectorIndex index;

    public VectorIndexTests() {
        decisions = new(db.Connection);
        index = new(db.Connection);
    }

    public void Dispose() => db.Dispose();

    [Fact]
    public void ShouldMatchBruteForceOrder_WhenIndexHasManyVectors() {
        Random random = new(seed);
        Dictionary<long, float[]> stored = [];
        for (int number = 0; number < vectorCount; number++) {
            long id = decisions.Record(Numbered(number));
            stored[id] = RandomVector(random);
            index.Add(id, stored[id]);
        }
        float[] query = RandomVector(random);

        IReadOnlyList<Neighbour> nearest = index.Nearest(query, k);

        List<(long Id, double Distance)> expected = [.. stored
            .Select(pair => (pair.Key, CosineDistance(query, pair.Value)))
            .OrderBy(pair => pair.Item2)
            .Take(k)];
        Assert.Equal(expected.Select(pair => pair.Id), nearest.Select(neighbour => neighbour.DecisionId));
        Assert.All(expected.Zip(nearest), pair => Assert.Equal(pair.First.Distance, pair.Second.Distance, tolerance));
    }

    [Fact]
    public void ShouldSkipVector_WhenDimensionDiffers() {
        long shorter = decisions.Record(Numbered(1));
        long matching = decisions.Record(Numbered(2));
        index.Add(shorter, [1f, 0f, 0f, 0f]);
        index.Add(matching, [1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f]);

        IReadOnlyList<Neighbour> nearest = index.Nearest([1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f], k);

        Assert.Equal([matching], nearest.Select(neighbour => neighbour.DecisionId));
    }

    [Fact]
    public void ShouldKeepLatestVector_WhenDecisionIsAddedAgain() {
        long id = decisions.Record(Primary);
        index.Add(id, [1f, 0f]);

        index.Add(id, [0f, 1f]);

        Neighbour only = Assert.Single(index.Nearest([0f, 1f], k));
        Assert.Equal(0f, only.Distance, tolerance);
    }

    [Fact]
    public void ShouldFindNothing_WhenIndexIsCleared() {
        index.Add(decisions.Record(Primary), [1f, 0f]);

        index.Clear();

        Assert.Empty(index.Nearest([1f, 0f], k));
    }

    [Fact]
    public void ShouldStoreLittleEndianFloat32_WhenVectorIsAdded() {
        index.Add(decisions.Record(Primary), [1f, -2f]);

        Assert.Equal([0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0xC0],
            (byte[]?)db.Scalar("SELECT embedding FROM decision_vectors"));
    }

    [Fact]
    public void ShouldSkipVector_WhenStoredVectorIsZero() {
        long zero = decisions.Record(Numbered(1));
        long matching = decisions.Record(Numbered(2));
        index.Add(zero, [0f, 0f]);
        index.Add(matching, [0f, 1f]);

        IReadOnlyList<Neighbour> nearest = index.Nearest([1f, 0f], k);

        Assert.Equal([matching], nearest.Select(neighbour => neighbour.DecisionId));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ShouldSkipVector_WhenStoredVectorIsNotFinite(float component) {
        long broken = decisions.Record(Numbered(1));
        long matching = decisions.Record(Numbered(2));
        index.Add(broken, [component, 1f]);
        index.Add(matching, [0f, 1f]);

        IReadOnlyList<Neighbour> nearest = index.Nearest([1f, 0f], k);

        Assert.Equal([matching], nearest.Select(neighbour => neighbour.DecisionId));
    }

    [Fact]
    public void ShouldFindNothing_WhenQueryIsZero() {
        index.Add(decisions.Record(Primary), [1f, 0f]);

        Assert.Empty(index.Nearest([0f, 0f], k));
    }

    private static float[] RandomVector(Random random) {
        float scale = random.Next(1, 10);
        return [.. Enumerable.Range(0, dimension).Select(_ => (float)(random.NextDouble() * 2 - 1) * scale)];
    }

    private static double CosineDistance(float[] left, float[] right) {
        double dot = 0;
        double leftSquares = 0;
        double rightSquares = 0;
        for (int position = 0; position < left.Length; position++) {
            dot += left[position] * right[position];
            leftSquares += left[position] * left[position];
            rightSquares += right[position] * right[position];
        }
        return 1 - dot / (Math.Sqrt(leftSquares) * Math.Sqrt(rightSquares));
    }
}
