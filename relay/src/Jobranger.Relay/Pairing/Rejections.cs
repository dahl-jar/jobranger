using Jobranger.Relay.Security;
using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobranger.Relay.Pairing;

internal static class Rejections
{
    private const string rejectionPrefix = "rejected:";

    public static ProblemHttpResult Reject(this ILogger logger, HttpContext http, RelayProblem problem)
    {
        if (logger.IsEnabled(LogLevel.Information)
            && http.RequestServices.GetRequiredService<RefusalSampler>().ShouldLog($"{rejectionPrefix}{problem.Code}"))
        {
            logger.RequestRejected(problem.Code);
        }

        return problem.ToResult();
    }
}
