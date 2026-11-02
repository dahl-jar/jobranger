using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Jobranger.Cli.Features.Delivery;

namespace Jobranger.Cli.Tests.Mocks;

internal static class SmtpCertificates {
    private const int keyBits = 2048;
    private const string subject = "CN=mail.example.test";

    public static readonly X509Certificate2 Server = Create();

    public static readonly DeliveryTransport Trusting = DeliveryTransport.Production with {
        IsTrusted = (certificate, errors) =>
            errors == SslPolicyErrors.None || certificate?.GetCertHashString() == Server.Thumbprint,
    };

    private static X509Certificate2 Create() {
        using RSA key = RSA.Create(keyBits);
        CertificateRequest request = new(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        SubjectAlternativeNameBuilder names = new();
        names.AddIpAddress(IPAddress.Loopback);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 made = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(made.Export(X509ContentType.Pfx), null);
    }
}
