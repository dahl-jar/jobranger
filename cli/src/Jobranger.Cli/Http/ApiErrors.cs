using System.Globalization;

namespace Jobranger.Cli.Http;

internal static class ApiErrors {
    private const int badRequest = 400;
    private const int unauthorized = 401;
    private const int forbidden = 403;
    private const int notFound = 404;
    private const int tooLarge = 413;
    private const int unprocessable = 422;
    private const int tooManyRequests = 429;
    private const int overloaded = 529;

    public static ApiErrorKind KindOf(int status, bool anthropic) => status switch {
        badRequest or tooLarge or unprocessable => ApiErrorKind.BadRequest,
        unauthorized or forbidden => ApiErrorKind.Auth,
        notFound => ApiErrorKind.NotFound,
        tooManyRequests => ApiErrorKind.RateLimit,
        overloaded when anthropic => ApiErrorKind.Overloaded,
        _ => ApiErrorKind.Server,
    };

    public static string Message(int status, string? detail) {
        string line = string.Create(CultureInfo.InvariantCulture, $"HTTP {status}");
        return string.IsNullOrWhiteSpace(detail) ? line : $"{line}: {detail}";
    }
}
