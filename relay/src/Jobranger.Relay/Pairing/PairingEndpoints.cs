using System.Security.Cryptography;
using Jobranger.Relay.Core;
using Jobranger.Relay.Security;
using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobranger.Relay.Pairing;

internal static class PairingEndpoints
{
    public static IEndpointRouteBuilder MapPairing(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder pair = app.MapGroup("/pair")
            .RequireCors(SiteCors.Policy)
            .AddEndpointFilter(new RequestVersionFilter());
        pair.MapPost("/nonce", IssueNonce).RequireFailureLimit(RateLimiting.Nonce);
        pair.MapPost("/start", StartPairing).RequireRelayLimit(RateLimiting.PairStart);
        pair.MapPost("/claim", ClaimPairing).RequireRelayLimit(RateLimiting.PairClaim);
        return app;
    }

    private static Results<Ok<NonceResponse>, ProblemHttpResult> IssueNonce(
        NonceRequest request,
        HttpContext http,
        NonceStore nonces,
        PairedChannels paired,
        RelayRateLimiter limiter,
        ILoggerFactory loggers)
    {
        ChannelId? channel = TouchPaired(request.Channel, paired);
        if (RateLimiting.Throttle(http, limiter, channel, RelayRole.Cli, RateLimiting.Nonce) is { } refused)
        {
            return refused;
        }

        RelayNonce? issued = channel is { } owner
            ? nonces.IssueForChannel(owner)
            : nonces.IssueForNetwork(ClientPartition.Network(http.Connection.RemoteIpAddress));
        if (issued is not { } nonce)
        {
            return RelayLog.For(loggers).Reject(http, RelayProblems.RelayFull);
        }

        return TypedResults.Ok(new NonceResponse(nonce.Value, Expiry.WholeSeconds(nonce.ExpiresAt)));
    }

    private static ChannelId? TouchPaired(string? channel, PairedChannels paired) =>
        channel is not null
            && CanonicalBase64Url.TryDecode(channel, SHA256.HashSizeInBytes, out _)
            && paired.Touch(new ChannelId(channel))
            ? new ChannelId(channel)
            : null;

    private static Results<Ok<PairStartResponse>, ProblemHttpResult> StartPairing(
        ProofRequest request,
        HttpContext http,
        NonceStore nonces,
        PairingStore pairings,
        ChannelHandle handles,
        TimeProvider time,
        ILoggerFactory loggers)
    {
        ILogger logger = RelayLog.For(loggers);
        ProofResult proof = ProofCheck.Check(request, OwnershipProof.PairStartPurpose, nonces);
        if (proof.Problem is { } problem)
        {
            return logger.Reject(http, problem);
        }

        if (pairings.Start(proof.Channel, ClientPartition.Network(http.Connection.RemoteIpAddress)) is not { } pending)
        {
            return logger.Reject(http, RelayProblems.RelayFull);
        }

        DateTimeOffset handleExpires = Expiry.After(time, RelayLimits.HandleLifetime);
        string handle = handles.Sign(proof.Channel, RelayRole.Cli, handleExpires);
        logger.PairingStarted(proof.Channel.Value);
        return TypedResults.Ok(new PairStartResponse(
            pending.Code.ToString(), Expiry.WholeSeconds(pending.ExpiresAt), proof.Channel.Value, handle, handleExpires));
    }

    private static Results<Ok<WebSessionResponse>, ProblemHttpResult> ClaimPairing(
        ClaimRequest request,
        HttpContext http,
        PairingStore pairings,
        PairedChannels paired,
        ChannelHandle handles,
        TimeProvider time,
        ILoggerFactory loggers)
    {
        ILogger logger = RelayLog.For(loggers);
        if (!PairingCode.TryParse(request.Code, out PairingCode code))
        {
            return logger.Reject(http, RelayProblems.InvalidCodeFormat);
        }

        ClaimResult claim = pairings.Claim(code);
        if (claim is not { Outcome: ClaimOutcome.Claimed, Channel: { } channel })
        {
            return logger.Reject(http, claim.Outcome == ClaimOutcome.Expired ? RelayProblems.CodeExpired : RelayProblems.WrongCode);
        }

        paired.Record(channel);
        logger.PairingClaimed(channel.Value);
        return TypedResults.Ok(WebSession.Open(channel, handles, time));
    }
}
