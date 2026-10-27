using System.Net;
using System.Security.Cryptography;
using Jobranger.Contracts.Channels;
using Jobranger.Contracts.Fields;
using Jobranger.Contracts.Proofs;
using Jobranger.Relay.Core;
using Jobranger.Relay.Security;
using Jobranger.Relay.Telemetry;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Jobranger.Relay.Pairing;

internal static class PairingEndpoints {
    public static IEndpointRouteBuilder MapPairing(this IEndpointRouteBuilder app) {
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
        ILoggerFactory loggers) {
        ChannelId? channel = ParseChannel(request.Channel);
        ProblemHttpResult? refused = channel switch {
            null => RateLimiting.Throttle(http, limiter.AttemptAcquireForAddress(http, RateLimiting.Nonce), RateLimiting.Nonce),
            { } owner when paired.Touch(owner) =>
                RateLimiting.Throttle(http, limiter.AttemptAcquireForChannelNonce(http, owner), RateLimiting.ChannelNonce),
            { } => RateLimiting.Throttle(http, limiter.AttemptAcquireForClient(http, RateLimiting.Nonce), RateLimiting.Nonce),
        };
        if (refused is not null) {
            return refused;
        }

        IPAddress? address = http.Connection.RemoteIpAddress;
        string network = ClientPartition.Network(address);
        RelayNonce? issued = channel is { } bound
            ? nonces.IssueForChannel(bound, ClientPartition.For(address), network)
            : nonces.IssueForNetwork(network);
        if (issued is not { } nonce) {
            return RelayLog.For(loggers).Reject(http, RelayProblems.RelayFull);
        }

        return TypedResults.Ok(new NonceResponse(nonce.Value, Expiry.WholeSeconds(nonce.ExpiresAt)));
    }

    private static ChannelId? ParseChannel(string? channel) =>
        channel is not null && CanonicalBase64Url.TryDecode(channel, SHA256.HashSizeInBytes, out _) ? new ChannelId(channel) : null;

    private static Results<Ok<PairStartResponse>, ProblemHttpResult> StartPairing(
        ProofRequest request,
        HttpContext http,
        NonceStore nonces,
        PairingStore pairings,
        ChannelHandle handles,
        TimeProvider time,
        RelayMetrics metrics,
        ILoggerFactory loggers) {
        ILogger logger = RelayLog.For(loggers);
        ProofResult proof = ProofCheck.Check(request, OwnershipProof.PairStartPurpose, nonces);
        if (proof.Problem is { } problem) {
            return logger.Reject(http, problem);
        }

        if (pairings.Start(proof.Channel, ClientPartition.Network(http.Connection.RemoteIpAddress)) is not { } pending) {
            return logger.Reject(http, RelayProblems.RelayFull);
        }

        DateTimeOffset handleExpires = Expiry.After(time, RelayLimits.HandleLifetime);
        string handle = handles.SignCli(proof.Channel, handleExpires);
        logger.PairingStarted(proof.Channel.Value);
        metrics.PairingStarted();
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
        RelayMetrics metrics,
        ILoggerFactory loggers) {
        ILogger logger = RelayLog.For(loggers);
        if (!PairingCode.TryParse(request.Code, out PairingCode code)) {
            return logger.Reject(http, RelayProblems.InvalidCodeFormat);
        }

        ClaimResult claim = pairings.Claim(code);
        if (claim is not { Outcome: ClaimOutcome.Claimed, Channel: { } channel }) {
            return logger.Reject(http, claim.Outcome == ClaimOutcome.Expired ? RelayProblems.CodeExpired : RelayProblems.WrongCode);
        }

        paired.Record(channel);
        logger.PairingClaimed(channel.Value);
        metrics.PairingClaimed();
        return TypedResults.Ok(WebSession.Open(channel, ChannelHandle.NewGrant(), handles, time));
    }
}
