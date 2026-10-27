namespace Jobranger.Cli.Http;

internal enum ApiErrorKind {
    BadRequest,
    Auth,
    NotFound,
    RateLimit,
    Overloaded,
    Server,
    Connection,
}
