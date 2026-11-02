using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Jobranger.Cli.Features.Delivery;

internal sealed record DeliveryTransport(
    Func<HttpMessageHandler> CreateResendHandler,
    Func<string, CancellationToken, Task<IPAddress[]>> Resolve,
    Func<X509Certificate?, SslPolicyErrors, bool> IsTrusted) {
    public static readonly DeliveryTransport Production = new(
        () => new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false, UseCookies = false },
        (host, token) => Dns.GetHostAddressesAsync(host, token),
        (_, errors) => errors == SslPolicyErrors.None);
}
