using Jobranger.Relay.Core;
using Jobranger.Relay.Security;
using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobranger.Relay.Pairing;

internal static class SessionEndpoints {
    public static IEndpointRouteBuilder MapSessions(this IEndpointRouteBuilder app) {
        RouteGroupBuilder session = app.MapGroup("/session")
            .RequireCors(SiteCors.Policy)
            .RequireFailureLimit(RateLimiting.Session)
            .AddEndpointFilter(new RequestVersionFilter());
        session.MapPost("/cli", StartCliSession);
        session.MapPost("/web", StartWebSession);
        return app;
    }

    private static Results<Ok<CliSessionResponse>, ProblemHttpResult> StartCliSession(
        ProofRequest request,
        HttpContext http,
        NonceStore nonces,
        PairedChannels paired,
        RelayRateLimiter limiter,
        ChannelHandle handles,
        TimeProvider time,
        ILoggerFactory loggers) {
        ILogger logger = RelayLog.For(loggers);
        ProofResult proof = ProofCheck.Check(request, OwnershipProof.SessionCliPurpose, nonces);
        if (proof.Problem is { } problem) {
            return Failed(http, logger, problem);
        }

        if (RateLimiting.ThrottleChannel(http, limiter, proof.Channel, RelayRole.Cli) is { } refused) {
            return refused;
        }

        paired.Record(proof.Channel);

        DateTimeOffset handleExpires = Expiry.After(time, RelayLimits.HandleLifetime);
        logger.SessionStarted(RelayRole.Cli, proof.Channel.Value);
        return TypedResults.Ok(new CliSessionResponse(
            proof.Channel.Value, handles.SignCli(proof.Channel, handleExpires), handleExpires));
    }

    private static Results<Ok<WebSessionResponse>, ProblemHttpResult> StartWebSession(
        TicketRequest request,
        HttpContext http,
        PairedChannels paired,
        RelayRateLimiter limiter,
        ChannelHandle handles,
        TimeProvider time,
        ILoggerFactory loggers) {
        ILogger logger = RelayLog.For(loggers);
        TicketCheck check = handles.VerifyTicket(request.Ticket);
        if (check is not { Outcome: TicketOutcome.Valid, Channel: { } channel, Grant: { } grant }) {
            return Failed(
                http, logger, check.Outcome == TicketOutcome.Expired ? RelayProblems.TicketExpired : RelayProblems.InvalidTicket);
        }

        paired.Record(channel);
        if (RateLimiting.ThrottleChannel(http, limiter, channel, RelayRole.Web) is { } refused) {
            return refused;
        }

        logger.SessionStarted(RelayRole.Web, channel.Value);
        return TypedResults.Ok(WebSession.Open(channel, grant, handles, time));
    }

    private static ProblemHttpResult Failed(HttpContext http, ILogger logger, RelayProblem problem) =>
        FailureCharge.Charge(http) ?? logger.Reject(http, problem);
}
