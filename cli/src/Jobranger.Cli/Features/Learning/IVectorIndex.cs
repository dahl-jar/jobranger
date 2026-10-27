namespace Jobranger.Cli.Features.Learning;

internal interface IVectorIndex {
    void Add(long decisionId, float[] vector);

    void Clear();

    IReadOnlyList<Neighbour> Nearest(float[] vector, int k);
}
