using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Nodes;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Http;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Delivery;

internal sealed class ResendSender(
    string? apiKey,
    string idempotencyKey,
    DeliveryTransport transport,
    IMessages messages,
    TimeProvider time) : IEmailSender {
    private const string endpoint = "https://api.resend.com/emails";
    private const string idempotencyHeader = "Idempotency-Key";
    private const string fromField = "from";
    private const string toField = "to";
    private const string subjectField = "subject";
    private const string htmlField = "html";
    private const string textField = "text";
    private const string timeoutMessage = "resend sent no response headers in time";
    private const char firstKeyCharacter = ' ';
    private const char lastKeyCharacter = '~';
    private const int redirectFirst = 300;
    private const int redirectLast = 399;
    private const int unauthorizedStatus = 401;
    private const int forbiddenStatus = 403;
    private const int validationStatus = 422;
    private const int maxReplyBytes = 64 * 1024;

    public async Task<SendFailure?> SendAsync(
        RenderedDigest rendered,
        EmailAddresses addresses,
        CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(apiKey)) {
            return new SendFailure(SendFailureKind.MissingResendKey, messages.EmailMissingResendKey);
        }
        if (apiKey.Any(character => character is < firstKeyCharacter or > lastKeyCharacter)) {
            return new SendFailure(SendFailureKind.UnusableResendKey, messages.EmailUnusableResendKey);
        }
        using CancellationTokenSource deadline = new(SendLimits.Deadline, time);
        using CancellationTokenSource limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        using HttpClient http = new(transport.CreateResendHandler(), disposeHandler: true) {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        using HttpRequestMessage request = Request(rendered, addresses);
        int status = 0;
        try {
            using HttpResponseMessage response = await PostAsync(http, request, limited.Token).ConfigureAwait(false);
            status = (int)response.StatusCode;
            return await FailureOfAsync(response, status, limited.Token).ConfigureAwait(false);
        } catch (Exception error) when (error
            is HttpRequestException or IOException or OperationCanceledException or TimeoutException or BodyTooLargeException) {
            cancellationToken.ThrowIfCancellationRequested();
            return Failure(error, deadline.IsCancellationRequested, status);
        }
    }

    private HttpRequestMessage Request(RenderedDigest rendered, EmailAddresses addresses) {
        JsonObject body = new() {
            [fromField] = addresses.Sender,
            [toField] = new JsonArray(JsonValue.Create(addresses.To)),
            [subjectField] = rendered.Subject,
            [htmlField] = rendered.Html,
            [textField] = rendered.Text,
        };
        HttpRequestMessage request = new(HttpMethod.Post, endpoint) {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, MediaTypeNames.Application.Json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(ProviderHosts.Bearer, apiKey);
        request.Headers.TryAddWithoutValidation(idempotencyHeader, idempotencyKey);
        request.Headers.ConnectionClose = true;
        return request;
    }

    private async Task<HttpResponseMessage> PostAsync(HttpClient http, HttpRequestMessage request, CancellationToken token) {
        using CancellationTokenSource timer = new(SendLimits.Operation, time);
        using CancellationTokenSource limited = CancellationTokenSource.CreateLinkedTokenSource(token, timer.Token);
        try {
            return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limited.Token).ConfigureAwait(false);
        } catch (OperationCanceledException error) when (timer.IsCancellationRequested && !token.IsCancellationRequested) {
            throw new TimeoutException(timeoutMessage, error);
        }
    }

    private async Task<SendFailure?> FailureOfAsync(HttpResponseMessage response, int status, CancellationToken token) {
        if (response.IsSuccessStatusCode) {
            return null;
        }
        if (status is >= redirectFirst and <= redirectLast) {
            return new SendFailure(SendFailureKind.ResendRedirect, messages.EmailResendRedirect(status), Status: status);
        }
        if (status is unauthorizedStatus or forbiddenStatus) {
            return new SendFailure(SendFailureKind.ResendKey, messages.EmailResendKeyRefused, Status: status);
        }
        if (status == validationStatus) {
            string reason = await RejectionAsync(response, token).ConfigureAwait(false);
            if (reason.Length > 0) {
                return new SendFailure(SendFailureKind.ResendRejected, reason, Status: status);
            }
        }
        return StatusFailure(status, nameof(SendFailure));
    }

    private async Task<string> RejectionAsync(HttpResponseMessage response, CancellationToken token) {
        Stream body = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using TimedStream timed = new(body, SendLimits.Operation, time);
        return ResendReply.Message(await CappedReader.ReadAsync(timed, maxReplyBytes, token).ConfigureAwait(false));
    }

    private SendFailure Failure(Exception error, bool deadlinePassed, int status) {
        string type = error.GetType().Name;
        if (deadlinePassed) {
            return new SendFailure(
                SendFailureKind.ResendTooSlow, messages.EmailResendTooSlow(SendLimits.DeadlineSeconds), type);
        }
        return error switch {
            TimeoutException => new SendFailure(SendFailureKind.ResendTimeout, messages.EmailResendTimeout, type),
            BodyTooLargeException => StatusFailure(status, type),
            _ => new SendFailure(SendFailureKind.ResendUnreachable, messages.EmailResendUnreachable, type),
        };
    }

    private SendFailure StatusFailure(int status, string errorType) =>
        new(SendFailureKind.ResendStatus, messages.EmailResendStatus(status), errorType, status);
}
