using System.Net;
using Jobranger.Cli.Http;

namespace Jobranger.Cli.Tests.Http;

public sealed class PublicAddressTests {
    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("10.1.2.3")]
    [InlineData("100.64.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.0.0.8")]
    [InlineData("192.0.2.1")]
    [InlineData("192.88.99.1")]
    [InlineData("192.168.1.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("::7f00:1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("64:ff9b:1::1")]
    [InlineData("100::1")]
    [InlineData("2001::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002::1")]
    [InlineData("3fff::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12::1")]
    [InlineData("fe80::1%1")]
    [InlineData("fec0::1")]
    [InlineData("ff02::1")]
    public void ShouldRefuseAddress_WhenRangeIsNotGlobal(string address) {
        Assert.False(PublicAddress.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("11.0.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("172.32.0.1")]
    [InlineData("223.255.255.255")]
    [InlineData("2606:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("64:ff9b::808:808")]
    public void ShouldAllowAddress_WhenRangeIsGlobal(string address) {
        Assert.True(PublicAddress.IsPublic(IPAddress.Parse(address)));
    }
}
