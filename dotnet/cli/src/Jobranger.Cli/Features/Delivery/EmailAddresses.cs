using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Delivery;

internal sealed record EmailAddresses(string Sender, string To) {
    private const string senderName = "jobranger";
    private const string defaultResendAddress = "onboarding@resend.dev";

    public static EmailAddresses? From(EmailConfig email) {
        if (email.Address is null || MailAddresses.AsciiAddress(email.Address) is not string to) {
            return null;
        }
        return MailAddresses.AsciiMailbox(SenderFor(email, to)) is string sender ? new EmailAddresses(sender, to) : null;
    }

    private static string SenderFor(EmailConfig email, string to) {
        if (!string.IsNullOrEmpty(email.Sender)) {
            return email.Sender;
        }
        return email.Via == EmailVia.Resend ? $"{senderName} <{defaultResendAddress}>" : $"{senderName} <{to}>";
    }
}
