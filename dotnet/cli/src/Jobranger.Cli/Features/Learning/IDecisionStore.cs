namespace Jobranger.Cli.Features.Learning;

internal interface IDecisionStore {
    long Record(NewDecision decision);

    long? Withdraw(string adId);

    IReadOnlyList<string>? Forget(long decisionId);

    IReadOnlyList<string> ForgetAll();

    IReadOnlyList<Decision> List();

    IReadOnlyDictionary<long, Decision> ById(IEnumerable<long> decisionIds);

    IReadOnlyList<Decision> PendingEmbeddings(string model);

    bool StoreEmbedding(long decisionId, string embedText, string model, float[] vector);

    long Count();

    IReadOnlyDictionary<long, int> UseCounts();

    bool RecordExamples(string adId, IReadOnlyList<long> decisionIds);
}
