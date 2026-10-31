using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Logging;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using AdsEntry = Jobranger.Cli.Features.Ads.Ads;
using DigestEntry = Jobranger.Cli.Features.Digest.Digest;
using RunsEntry = Jobranger.Cli.Features.Runs.Runs;

namespace Jobranger.Cli.Features.Delivery;

[UnsupportedOSPlatform("windows")]
internal sealed class Delivery(
    SqliteConnection connection,
    Credentials credentials,
    ServiceUrls urls,
    IMessages messages,
    DeliveryTransport transport,
    TimeProvider time,
    ILogger logger) {
    private const string smtpSecret = "smtp_password";
    private const string resendSecret = "resend_api_key";
    private const int recentRuns = 2;

    private static readonly IMessages digestMessages = new EnglishMessages();

    public async Task<DeliveryResult> DeliverAsync(AppConfig config, RunResult result, CancellationToken cancellationToken) {
        if (result.Status is not (RunStatus.Completed or RunStatus.Failed)) {
            return new DeliveryResult();
        }
        DateTimeOffset now = time.GetLocalNow();
        AdsEntry ads = new(connection, time);
        ads.MarkReported(result.SilentIds, now);
        EmailConfig email = config.Notify.Email;
        if (!email.On) {
            ads.MarkReported(result.ReportedIds, now);
            return new DeliveryResult();
        }
        DigestInput input = DeliveryCards.Input(ads, RunsEntry.Recent(connection, recentRuns), result, config, now);
        DigestData data = DigestEntry.Build(input, DateOnly.FromDateTime(now.DateTime));
        if (!HasNews(data)) {
            return new DeliveryResult();
        }
        RenderedDigest rendered = DigestEntry.Render(
            data, config.Notify.Website ? urls.Site.AbsoluteUri : null, digestMessages);
        SendFailure? failure = await SendAsync(email, rendered, result.RunId, cancellationToken).ConfigureAwait(false);
        if (failure is not null) {
            LogEvents.EmailFailed(logger, failure.ErrorType, LogFields.FieldName(failure.Kind.ToString()), failure.Status);
            return new DeliveryResult(EmailError: failure.Reason);
        }
        ads.MarkReported(rendered.AdIds, now);
        LogEvents.EmailSent(logger, rendered.AdIds.Count);
        return new DeliveryResult(EmailTo: email.Address);
    }

    private static bool HasNews(DigestData data) =>
        data.ApplySoon.Count > 0
        || data.NewToday.Count > 0
        || data.Carried.Count > 0
        || data.FailedSources.Count > 0
        || data.BudgetPaused
        || data.ModelStopped is not null;

    private async Task<SendFailure?> SendAsync(
        EmailConfig email, RenderedDigest rendered, long? runId, CancellationToken cancellationToken) {
        string? secret;
        try {
            secret = await credentials
                .GetSecretAsync(email.Via == EmailVia.Resend ? resendSecret : smtpSecret, cancellationToken)
                .ConfigureAwait(false);
        } catch (SecretStoreUnavailableException) {
            return new SendFailure(SendFailureKind.SecretStore, messages.EmailSecretStore, nameof(SecretStoreUnavailableException));
        }
        if (email.Address is null) {
            return new SendFailure(SendFailureKind.MissingAddress, messages.EmailMissingAddress);
        }
        if (EmailAddresses.From(email) is not EmailAddresses addresses) {
            return new SendFailure(SendFailureKind.UnusableAddress, messages.EmailUnusableAddress);
        }
        return email.Via == EmailVia.Resend
            ? await SendThroughResendAsync(secret, rendered, addresses, runId, cancellationToken).ConfigureAwait(false)
            : await SendThroughSmtpAsync(email, secret, rendered, addresses, cancellationToken).ConfigureAwait(false);
    }

    private Task<SendFailure?> SendThroughResendAsync(
        string? key, RenderedDigest rendered, EmailAddresses addresses, long? runId, CancellationToken cancellationToken) {
        if (string.IsNullOrEmpty(key)) {
            return Task.FromResult<SendFailure?>(new SendFailure(SendFailureKind.MissingResendKey, messages.EmailMissingResendKey));
        }
        ResendSender sender = new(transport, messages, time);
        return sender.SendAsync(key, rendered, addresses, IdempotencyKey.For(runId, rendered, addresses), cancellationToken);
    }

    private Task<SendFailure?> SendThroughSmtpAsync(
        EmailConfig email, string? password, RenderedDigest rendered, EmailAddresses addresses, CancellationToken cancellationToken) {
        if (email.SmtpUrl is null) {
            return Task.FromResult<SendFailure?>(new SendFailure(SendFailureKind.MissingSmtpUrl, messages.EmailMissingSmtpUrl));
        }
        SmtpSender sender = new(transport, messages, time);
        return sender.SendAsync(email.SmtpUrl, email.SmtpUser, password, rendered, addresses, cancellationToken);
    }
}
