using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Digest;
using Jobranger.Cli.Features.Runs;
using Jobranger.Cli.Files;
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
        DigestInput input = DigestInputs.From(ads, RunsEntry.Recent(connection, recentRuns), result, config, now);
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
        (string? secretName, Func<string?, EmailAddresses, IEmailSender> createSender) = SenderFor(email, rendered, runId);
        string? secret;
        try {
            secret = secretName is null
                ? null
                : await credentials.GetSecretAsync(secretName, cancellationToken).ConfigureAwait(false);
        } catch (Exception error) when (error is SecretStoreUnavailableException or PrivateFileException) {
            return new SendFailure(SendFailureKind.SecretStore, messages.EmailSecretStore, error.GetType().Name);
        }
        if (email.Address is null) {
            return new SendFailure(SendFailureKind.MissingAddress, messages.EmailMissingAddress);
        }
        if (EmailAddresses.From(email) is not EmailAddresses addresses) {
            return new SendFailure(SendFailureKind.UnusableAddress, messages.EmailUnusableAddress);
        }
        IEmailSender sender = createSender(secret, addresses);
        return await sender.SendAsync(rendered, addresses, cancellationToken).ConfigureAwait(false);
    }

    private (string? SecretName, Func<string?, EmailAddresses, IEmailSender> CreateSender) SenderFor(
        EmailConfig email, RenderedDigest rendered, long? runId) => email.Via == EmailVia.Resend
            ? (resendSecret, (apiKey, addresses) =>
                new ResendSender(apiKey, IdempotencyKey.For(runId, rendered, addresses), transport, messages, time))
            : (string.IsNullOrEmpty(email.SmtpUser) ? null : smtpSecret,
                (password, _) => new SmtpSender(email.SmtpUrl, email.SmtpUser, password, transport, messages, time));
}
