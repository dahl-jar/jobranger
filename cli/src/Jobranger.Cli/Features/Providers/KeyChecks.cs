using Jobranger.Cli.Http;

namespace Jobranger.Cli.Features.Providers;

internal static class KeyChecks {
    public static KeyCheck Rejected(ApiReply reply, bool anthropic, string label, string model, string officialKey) {
        ApiErrorKind kind = ApiErrors.KindOf(reply.Status, anthropic);
        string message = kind switch {
            ApiErrorKind.Auth => $"{label} rejected the key",
            ApiErrorKind.NotFound => $"{label} has no model named {model}",
            ApiErrorKind.BadRequest => BadRequest(reply, label, officialKey),
            _ => throw ApiErrors.ToException(reply, anthropic, officialKey),
        };
        return new KeyCheck(false, message);
    }

    private static string BadRequest(ApiReply reply, string label, string officialKey) {
        string message = $"{label} refused the key check. Check the key and its workspace or project settings";
        string? detail = ApiErrors.Detail(reply.Body, officialKey);
        return detail is null ? message : $"{message}. {label} said: {detail}";
    }
}
