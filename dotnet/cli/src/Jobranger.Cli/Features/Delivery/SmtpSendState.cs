using Jobranger.Cli.Config;

namespace Jobranger.Cli.Features.Delivery;

internal sealed record SmtpSendState(
    SmtpServer Server,
    EmailAddresses Addresses,
    bool DeadlinePassed,
    bool ReplyTooLong,
    bool NonReply,
    bool TimedOut,
    bool CertificateRefused,
    bool Connected);
