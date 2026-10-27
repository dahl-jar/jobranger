using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobranger.Relay.Endpoints;

internal sealed record RelayProblem(int Status, string Code) {
    public ProblemHttpResult ToResult() => TypedResults.Problem(
        statusCode: Status,
        title: Code,
        extensions: new Dictionary<string, object?> { [RelayProblems.CodeKey] = Code });
}
