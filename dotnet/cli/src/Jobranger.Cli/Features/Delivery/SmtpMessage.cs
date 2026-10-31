using Jobranger.Cli.Features.Digest;
using MimeKit;
using MimeKit.Utils;

namespace Jobranger.Cli.Features.Delivery;

internal static class SmtpMessage {
    public static MimeMessage Build(RenderedDigest rendered, EmailAddresses addresses, DateTimeOffset date) {
        MailboxAddress sender = MailboxAddress.Parse(addresses.Sender);
        MimeMessage message = new() {
            Subject = rendered.Subject,
            Date = date,
            MessageId = MimeUtils.GenerateMessageId(sender.Domain),
            Body = new BodyBuilder { TextBody = rendered.Text, HtmlBody = rendered.Html }.ToMessageBody(),
        };
        message.From.Add(sender);
        message.To.Add(MailboxAddress.Parse(addresses.To));
        return message;
    }
}
