using Jobranger.Cli.Features.Digest;

namespace Jobranger.Cli.Features.Delivery;

internal interface IEmailSender {
    Task<SendFailure?> SendAsync(RenderedDigest rendered, EmailAddresses addresses, CancellationToken cancellationToken);
}
