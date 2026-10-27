using Jobranger.Relay.Telemetry;

namespace Jobranger.Relay.Endpoints;

internal static class RelayProblems {
    public const string CodeKey = "code";

    private const string traceIdKey = "traceId";
    private const string tooManyRequestsType = "https://tools.ietf.org/html/rfc6585#section-4";

    public static readonly RelayProblem InvalidRequest = new(StatusCodes.Status400BadRequest, "invalid_request");
    public static readonly RelayProblem UnsupportedVersion = new(StatusCodes.Status400BadRequest, "unsupported_version");
    public static readonly RelayProblem InvalidCodeFormat = new(StatusCodes.Status400BadRequest, "invalid_code_format");
    public static readonly RelayProblem WrongCode = new(StatusCodes.Status404NotFound, "wrong_code");
    public static readonly RelayProblem CodeExpired = new(StatusCodes.Status410Gone, "code_expired");
    public static readonly RelayProblem InvalidNonce = new(StatusCodes.Status400BadRequest, "invalid_nonce");
    public static readonly RelayProblem NonceExpired = new(StatusCodes.Status410Gone, "nonce_expired");
    public static readonly RelayProblem InvalidSignature = new(StatusCodes.Status400BadRequest, "invalid_signature");
    public static readonly RelayProblem InvalidTicket = new(StatusCodes.Status400BadRequest, "invalid_ticket");
    public static readonly RelayProblem TicketExpired = new(StatusCodes.Status410Gone, "ticket_expired");
    public static readonly RelayProblem MissingHandle = new(StatusCodes.Status401Unauthorized, "missing_handle");
    public static readonly RelayProblem InvalidHandle = new(StatusCodes.Status401Unauthorized, "invalid_handle");
    public static readonly RelayProblem WrongOrigin = new(StatusCodes.Status401Unauthorized, "wrong_origin");
    public static readonly RelayProblem RateLimited = new(StatusCodes.Status429TooManyRequests, "rate_limited");
    public static readonly RelayProblem RelayFull = new(StatusCodes.Status503ServiceUnavailable, "relay_full");

    public static void Customize(ProblemDetailsContext context) {
        context.ProblemDetails.Extensions.Remove(traceIdKey);
        context.ProblemDetails.Extensions.TryAdd(CodeKey, CodeFor(context.ProblemDetails.Status));
        if (context.ProblemDetails.Extensions[CodeKey] is string code) {
            context.HttpContext.RequestServices.GetRequiredService<RelayMetrics>().RequestRejected(code);
        }

        if (context.ProblemDetails.Status == StatusCodes.Status429TooManyRequests) {
            context.ProblemDetails.Type ??= tooManyRequestsType;
        }
    }

    private static string CodeFor(int? status) => status switch {
        StatusCodes.Status400BadRequest => InvalidRequest.Code,
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status413PayloadTooLarge => "body_too_large",
        StatusCodes.Status415UnsupportedMediaType => "unsupported_media_type",
        _ => "internal_error",
    };
}
