using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Platform;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Text;

namespace Jobranger.Cli.Features.Setup;

[UnsupportedOSPlatform("windows")]
internal sealed class DeliveryStep(SetupContext context, SetupSecrets secrets) : ISetupStep {
    private const string timeJoin = ", ";
    private const char hourSeparator = ':';
    private const char hourPad = '0';
    private const int singleDigitHour = 1;

    private static readonly char[] timeSeparators = [',', ' '];

    private readonly IMessages messages = context.Messages;

    public string Title => messages.InitDeliveryTitle;

    public async Task<InitState> RunAsync(InitState state, CancellationToken cancellationToken) {
        IReadOnlyList<string> channels = await context.PickAtLeastOneAsync(
            messages.InitChannels,
            [
                new PromptChoice(ConfigKeys.Website, messages.InitChannelWebsite, true),
                new PromptChoice(ConfigKeys.Email, messages.InitChannelEmail, true),
            ],
            cancellationToken).ConfigureAwait(false);
        InitState next = channels.Contains(ConfigKeys.Email)
            ? await AskEmailAsync(state, cancellationToken).ConfigureAwait(false)
            : state;
        IReadOnlyList<string> times = await AskRunTimesAsync(next.Config, cancellationToken).ConfigureAwait(false);
        return next with {
            Config = next.Config with {
                Notify = next.Config.Notify with { Website = channels.Contains(ConfigKeys.Website) },
                Schedule = new ScheduleConfig { Times = times },
            },
        };
    }

    private static string Padded(string time) =>
        time.IndexOf(hourSeparator, StringComparison.Ordinal) == singleDigitHour ? hourPad + time : time;

    private async Task<InitState> AskEmailAsync(InitState state, CancellationToken cancellationToken) {
        string address = await AskValidAsync(
            messages.InitAddress, null, MailAddresses.IsAddress, messages.InitNotAddress, cancellationToken).ConfigureAwait(false);
        string server = SmtpDefaults.Server(address);
        PromptChoice[] choices = [
            new PromptChoice(WireNames.Of(EmailVia.Smtp), messages.InitViaSmtp),
            new PromptChoice(WireNames.Of(EmailVia.Resend), messages.InitViaResend),
        ];
        while (true) {
            string via = await context.Prompter.SelectAsync(messages.InitSendThrough, choices, cancellationToken)
                .ConfigureAwait(false);
            MailTransport transport;
            if (WireNames.Parse<EmailVia>(via) == EmailVia.Resend) {
                transport = await AskResendAsync(address, cancellationToken).ConfigureAwait(false);
            } else {
                server = await AskValidAsync(
                    messages.InitSmtpServer,
                    server,
                    text => SmtpServer.UrlFor(text) is not null,
                    messages.InitNotSmtpServer,
                    cancellationToken).ConfigureAwait(false);
                transport = await AskSmtpAsync(address, server, cancellationToken).ConfigureAwait(false);
            }
            string? reason = await context.Seams.SendTestEmail(transport.Email, transport.Secret, cancellationToken)
                .ConfigureAwait(false);
            if (reason is null) {
                await context.Output.WriteLineAsync(messages.InitTestSent(address)).ConfigureAwait(false);
                return (state with {
                    Config = state.Config with { Notify = state.Config.Notify with { Email = transport.Email } },
                }).WithSecret(transport.SecretName, transport.Secret);
            }
            await context.Output.WriteLineAsync(messages.InitTestFailed(reason)).ConfigureAwait(false);
        }
    }

    private async Task<MailTransport> AskSmtpAsync(string address, string server, CancellationToken cancellationToken) {
        string user = await context.Prompter.TextAsync(messages.InitSmtpUser, address, cancellationToken).ConfigureAwait(false);
        string password = await secrets.AskRequiredAsync(
            Credentials.SmtpPasswordName, messages.InitSmtpPassword, messages.InitEnterSmtpPassword, cancellationToken)
            .ConfigureAwait(false);
        EmailConfig email = new() {
            On = true,
            Address = address,
            Via = EmailVia.Smtp,
            SmtpUrl = SmtpServer.UrlFor(server),
            SmtpUser = user,
        };
        return new MailTransport(email, Credentials.SmtpPasswordName, password);
    }

    private async Task<MailTransport> AskResendAsync(string address, CancellationToken cancellationToken) {
        string key = await secrets.AskRequiredAsync(
            Credentials.ResendKeyName, messages.InitResendKey, messages.InitEnterResendKey, cancellationToken)
            .ConfigureAwait(false);
        string sender = await AskValidAsync(
            messages.InitSendFrom,
            MailAddresses.DefaultResendSender,
            MailAddresses.IsMailbox,
            messages.InitNotSender,
            cancellationToken).ConfigureAwait(false);
        EmailConfig email = new() { On = true, Address = address, Via = EmailVia.Resend, Sender = sender };
        return new MailTransport(email, Credentials.ResendKeyName, key);
    }

    private async Task<string> AskValidAsync(
        string message, string? defaultValue, Func<string, bool> isValid, string refusal, CancellationToken cancellationToken) {
        while (true) {
            string answer = await context.Prompter.TextAsync(message, defaultValue, cancellationToken).ConfigureAwait(false);
            if (isValid(answer)) {
                return answer;
            }
            await context.Output.WriteLineAsync(refusal).ConfigureAwait(false);
        }
    }

    private async Task<IReadOnlyList<string>> AskRunTimesAsync(AppConfig config, CancellationToken cancellationToken) {
        string suggested = string.Join(timeJoin, new ScheduleConfig().Times);
        while (true) {
            string answer = await context.Prompter.TextAsync(messages.InitRunTimes, suggested, cancellationToken)
                .ConfigureAwait(false);
            string[] times = [.. answer.Split(timeSeparators, StringSplitOptions.RemoveEmptyEntries).Select(Padded)];
            List<string> refusals = RunTimeRefusals(config, answer, times);
            if (refusals.Count == 0) {
                return times;
            }
            foreach (string refusal in refusals) {
                await context.Output.WriteLineAsync(refusal).ConfigureAwait(false);
            }
        }
    }

    private List<string> RunTimeRefusals(AppConfig config, string answer, string[] times) {
        if (times.Length == 0) {
            return [messages.InitNotRunTime(answer)];
        }
        List<string> refusals = [
            .. times.Where(time => !AppConfigValidator.IsRunTime(time)).Distinct().Select(messages.InitNotRunTime),
        ];
        bool tooClose = AppConfigValidator.Validate(config with { Schedule = new ScheduleConfig { Times = times } })
            .Any(error => error.Problem == ConfigProblem.RunTimesTooClose);
        if (tooClose) {
            refusals.Add(messages.InitRunTimesTooClose(AppConfigValidator.MinRunGapHours));
        }
        return refusals;
    }

    private sealed record MailTransport(EmailConfig Email, string SecretName, string Secret);
}
