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
        IReadOnlyList<string> how = await context.PickAtLeastOneAsync(
            messages.InitHow,
            [
                new PromptChoice(ConfigKeys.Website, messages.InitChannelWebsite, true),
                new PromptChoice(ConfigKeys.Email, messages.InitChannelEmail, true),
            ],
            cancellationToken).ConfigureAwait(false);
        InitState next = how.Contains(ConfigKeys.Email)
            ? await AskEmailAsync(state, cancellationToken).ConfigureAwait(false)
            : state;
        IReadOnlyList<string> times = await AskRunTimesAsync(next.Config, cancellationToken).ConfigureAwait(false);
        return next with {
            Config = next.Config with {
                Notify = next.Config.Notify with { Website = how.Contains(ConfigKeys.Website) },
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
                    text => SmtpDefaults.Url(text) is not null,
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
        string password = await secrets.AskAsync(Credentials.SmtpPasswordName, messages.InitSmtpPassword, cancellationToken)
            .ConfigureAwait(false);
        EmailConfig email = new() {
            On = true,
            Address = address,
            Via = EmailVia.Smtp,
            SmtpUrl = SmtpDefaults.Url(server),
            SmtpUser = user,
        };
        return new MailTransport(email, Credentials.SmtpPasswordName, password);
    }

    private async Task<MailTransport> AskResendAsync(string address, CancellationToken cancellationToken) {
        string key = await secrets.AskAsync(Credentials.ResendKeyName, messages.InitResendKey, cancellationToken)
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
            if (times.Length == 0) {
                await context.Output.WriteLineAsync(messages.InitRunTimesWrong(messages.ConfigRunTimeFormat))
                    .ConfigureAwait(false);
                continue;
            }
            ConfigError[] problems = [
                .. AppConfigValidator.Validate(config with { Schedule = new ScheduleConfig { Times = times } })
                    .Where(error => error.Problem is ConfigProblem.RunTimeFormat or ConfigProblem.RunTimesTooClose),
            ];
            if (problems.Length == 0) {
                return times;
            }
            foreach (ConfigError problem in problems) {
                await context.Output.WriteLineAsync(messages.InitRunTimesWrong(problem.Describe(messages))).ConfigureAwait(false);
            }
        }
    }

    private sealed record MailTransport(EmailConfig Email, string SecretName, string Secret);
}
