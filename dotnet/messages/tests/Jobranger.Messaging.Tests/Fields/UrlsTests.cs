using Jobranger.Messaging.Fields;
using Jobranger.Messaging.Limits;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Tests.Fields;

public sealed class UrlsTests {
    private const string prefix = "https://jobs.example.test/";
    private const int ligatureExpansion = 18;
    private const string hexPrefix = "0x";
    private const string lastDigit = "1";
    private static readonly string maxLengthUrl = prefix + new string('a', MessageLimits.UrlMaxChars - prefix.Length);
    private static readonly string longIpv4 = hexPrefix + new string('0', MessageLimits.UrlMaxChars - hexPrefix.Length - lastDigit.Length) + lastDigit;
    private static readonly string ignoredPadding = new('\u00ad', MessageLimits.UrlMaxChars);
    private static readonly string expandingLigatures = new('\ufdfa', (MessageLimits.UrlMaxChars / ligatureExpansion) + 1);

    public static TheoryData<string, string> EncodedUrls => new() {
        { "https://jobs.example.test/stilling/utvikler bø", "https://jobs.example.test/stilling/utvikler%20b%C3%B8" },
        { "https://jobs.example.test/a?q=b c#f g", "https://jobs.example.test/a?q=b%20c#f%20g" },
        { "https://jobs.example.test/a%20b?q=1", "https://jobs.example.test/a%20b?q=1" },
        { "https://bø.example.test/a", "https://xn--b-5ga.example.test/a" },
        { "http://[::1]:8080/a", "http://[::1]:8080/a" },
        { "http://user@[::1]/a", "http://user@[::1]/a" },
        { "http://[::FFFF:127.0.0.1]/", "http://[::ffff:7f00:1]/" },
        { "https://a@b@jobs.example.test/", "https://a@b@jobs.example.test/" },
        { "https://user:pw@jobs.example.test:0443/", "https://user:pw@jobs.example.test:443/" },
        { "http://[1:0:0:2:0:0:0:3]/", "http://[1:0:0:2::3]/" },
        { "http://[::]/", "http://[::]/" },
        { "http://[1::]/", "http://[1::]/" },
        { "http://[1:2:3:4:5:6:7:8]/", "http://[1:2:3:4:5:6:7:8]/" },
        { "http://[::1]:/", "http://[::1]/" },
        { "https://jobs.example.test:/", "https://jobs.example.test/" },
        { "https://jobs.example.test?q=1", "https://jobs.example.test?q=1" },
        { "https://jobs.example.test/#f", "https://jobs.example.test/#f" },
        { "https://jobs.example.test/?", "https://jobs.example.test/" },
        { "https://jobs.example.test/~a\u007f", "https://jobs.example.test/~a%7F" },
        { " \thttps://jobs.exam\rple.test/a\nb", "https://jobs.example.test/ab" },
        { "https://jobs.example.test#f", "https://jobs.example.test#f" },
        { "https://jobs.example.test", "https://jobs.example.test" },
        { "https://jobs.example.test:65535/!", "https://jobs.example.test:65535/!" },
        { maxLengthUrl, maxLengthUrl },
    };

    public static TheoryData<string, string> RejectedUrls => new() {
        { "javascript:alert(1)", "scheme" },
        { maxLengthUrl + "a", "maxLength" },
        { "https:///a", "host" },
        { "https://#@jobs.example.test", "host" },
        { "https:jobs.example.test/", "host" },
        { "jobs.example.test/", "scheme" },
        { "http://::1]/", "ipv6" },
        { "http://a[::1]/", "ipv6" },
        { "http://[::1", "ipv6" },
        { "http://[::1]x/", "ipv6" },
        { "https://[::1%25eth0]/", "ipv6" },
        { "http://[1.2.3.4]/", "ipv6" },
        { "http://[1:::2]/", "ipv6" },
        { "https://jobs.example.test/\ud800", "utf8" },
        { "http://exa mple.test/", "host" },
        { "https://-a.test/", "host" },
        { "https://a-.test/", "host" },
        { "https://user:pa ss@jobs.example.test/", "userinfo" },
        { "https://@jobs.example.test/", "userinfo" },
        { "https://\u00fc@jobs.example.test/", "userinfo" },
        { "https://attacker.example.test\\@jobs.example.test/", "userinfo" },
        { "https://jobs.example.test:99999/", "port" },
        { "https://jobs.example.test:0/", "port" },
        { "https://jobs.example.test:8a/", "port" },
    };

    public static TheoryData<string, string> WhatwgHosts => new() {
        { "https://faß.example.test/", "https://xn--fa-hia.example.test/" },
        { "https://ς.example.test/", "https://xn--3xa.example.test/" },
        { "https://0x7f.1/", "https://127.0.0.1/" },
        { "https://XN--FA-HIA.example.test/", "https://xn--fa-hia.example.test/" },
        { "https://0300.0250.1.1/", "https://192.168.1.1/" },
        { "https://4294967295/", "https://255.255.255.255/" },
        { "https://0x/", "https://0.0.0.0/" },
        { "https://1.2.3.4./", "https://1.2.3.4/" },
        { "https://127.1/", "https://127.0.0.1/" },
        { "https://127.0.1/", "https://127.0.0.1/" },
        { "https://0xC0A80101/", "https://192.168.1.1/" },
        { "https://\ud840\udc00.example.test/", "https://xn--j50i.example.test/" },
        { "https://ÿ.example.test/", "https://xn--wda.example.test/" },
        { "https://àá.example.test/", "https://xn--0cac.example.test/" },
        { "https://§.example.test/", "https://xn--eba.example.test/" },
        { "https://jobs.1a/", "https://jobs.1a/" },
        { "https://xn--z09-joa.example.test/", "https://xn--z09-joa.example.test/" },
        { "https://xn--fiq06l2rdsvsr3ccvkb1zc23aggs.example.test/", "https://xn--fiq06l2rdsvsr3ccvkb1zc23aggs.example.test/" },
        { "https://xn--d1abiccdbdkva0anafndi1o.example.test/", "https://xn--d1abiccdbdkva0anafndi1o.example.test/" },
        { "https://ジョブランジャーテスト.example.test/", "https://xn--yckad5cl4f9d3at4hre.example.test/" },
        { "https://ØST.example.test/", "https://xn--st-kka.example.test/" },
        { "https://\u13a0.example.test/", "https://xn--58d.example.test/" },
        { "https://\uab70.example.test/", "https://xn--58d.example.test/" },
        { "https://b\u00fc\u0301.example.test/", "https://xn--b-yta.example.test/" },
        { "https://e\u0301.example.test/", "https://xn--9ca.example.test/" },
        { "https://\u1112\u1161\u11ab.example.test/", "https://xn--6q8b.example.test/" },
        { $"https://{longIpv4}/", "https://0.0.0.1/" },
    };

    public static TheoryData<string, string> RejectedHosts => new() {
        { "https://1.2.3.4.5/", "ipv4" },
        { "https://1.2.3.4.0/", "ipv4" },
        { "https://256.1.1.1/", "ipv4" },
        { "https://4294967296/", "ipv4" },
        { "https://1.2.3.09/", "ipv4" },
        { "https://1.2.3.256/", "ipv4" },
        { "https://xn--a.example.test/", "host" },
        { "https://xn--abc-.example.test/", "host" },
        { "https://xn--wca.example.test/", "host" },
        { "https://xn--e-xbb.example.test/", "host" },
        { "https://a../", "host" },
        { "https://xn--ab_c.example.test/", "host" },
        { "https://1..2/", "ipv4" },
        { "https://a\u200db.example.test/", "host" },
        { "https://xn--ab-m1t.example.test/", "host" },
        { "https://x\u0301.example.test/", "host" },
        { "https://\u05d0.example.test/", "host" },
        { "https://xn--ypd8q.example.test/", "host" },
        { "https://\ud7a4\u11b3.example.test/", "host" },
    };

    public static TheoryData<string> OversizedHosts => new() {
        $"https://{ignoredPadding}jobs.example.test/",
        $"https://{expandingLigatures}.example.test/",
        $"https://{longIpv4}0/",
    };

    [Theory]
    [MemberData(nameof(EncodedUrls))]
    public void ShouldNormalizeUrl_WhenUrlIsValid(string url, string expected) {
        Assert.Equal(expected, Urls.Encode(url));
    }

    [Theory]
    [MemberData(nameof(RejectedUrls))]
    public void ShouldRejectUrl_WhenPartBreaksRule(string url, string keyword) {
        ContractException error = Assert.Throws<ContractException>(() => Urls.Encode(url));

        Assert.Equal(keyword, error.Keyword);
    }

    [Theory]
    [MemberData(nameof(WhatwgHosts))]
    public void ShouldMapHostLikeWhatwg_WhenUrlHasUts46OrIpv4Form(string url, string expected) {
        Assert.Equal(expected, Urls.Encode(url));
    }

    [Theory]
    [MemberData(nameof(RejectedHosts))]
    public void ShouldRejectHost_WhenBrowserWouldFailOrMapItElsewhere(string url, string keyword) {
        ContractException error = Assert.Throws<ContractException>(() => Urls.Encode(url));

        Assert.Equal(keyword, error.Keyword);
    }

    [Theory]
    [MemberData(nameof(OversizedHosts))]
    public void ShouldRejectHost_WhenHostRunesPassUrlMaxChars(string url) {
        ContractException error = Assert.Throws<ContractException>(() => Urls.Encode(url));

        Assert.Equal(FailureKeywords.MaxLength, error.Keyword);
    }
}
