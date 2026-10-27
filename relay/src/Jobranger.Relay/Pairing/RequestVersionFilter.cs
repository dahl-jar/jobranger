using Jobranger.Relay.Security;

namespace Jobranger.Relay.Pairing;

internal sealed class RequestVersionFilter : IEndpointFilter {
    private const int supportedVersion = 1;

    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next) =>
        context.Arguments.OfType<IVersionedRequest>().FirstOrDefault()?.V switch {
            null => Rejected(context.HttpContext, RelayProblems.InvalidRequest),
            supportedVersion => next(context),
            _ => Rejected(context.HttpContext, RelayProblems.UnsupportedVersion),
        };

    private static ValueTask<object?> Rejected(HttpContext http, RelayProblem problem) =>
        ValueTask.FromResult<object?>(FailureCharge.Charge(http) ?? problem.ToResult());
}
