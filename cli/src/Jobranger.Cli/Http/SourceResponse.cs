using System.Net;

namespace Jobranger.Cli.Http;

internal sealed record SourceResponse(HttpStatusCode Status, Uri Url, byte[] Body, string? CharSet);
