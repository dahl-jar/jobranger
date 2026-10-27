using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobranger.Relay.Pairing;

internal sealed record RelayProblem(int Status, string Code)
{
    public ProblemHttpResult ToResult() => TypedResults.Problem(
        statusCode: Status,
        title: Code,
        extensions: new Dictionary<string, object?> { [RelayProblems.CodeKey] = Code });
}
