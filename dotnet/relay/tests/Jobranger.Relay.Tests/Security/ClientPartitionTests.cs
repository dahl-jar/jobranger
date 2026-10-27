using System.Net;
using Jobranger.Relay.Security;

namespace Jobranger.Relay.Tests.Security;

public sealed class ClientPartitionTests {
    [Fact]
    public void ShouldGroupIpv6AddressesByPrefix64() {
        string key = ClientPartition.For(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd"));

        Assert.Equal("2001:db8:1:2::/64", key);
    }

    [Fact]
    public void ShouldKeepIpv4AddressAsIs() {
        string key = ClientPartition.For(IPAddress.Parse("198.51.100.7"));

        Assert.Equal("198.51.100.7", key);
    }

    [Fact]
    public void ShouldUnwrapIpv4MappedIpv6Address() {
        string key = ClientPartition.For(IPAddress.Parse("::ffff:198.51.100.7"));

        Assert.Equal("198.51.100.7", key);
    }

    [Fact]
    public void ShouldGroupUnknownAddressTogether() {
        string key = ClientPartition.For((IPAddress?)null);

        Assert.Equal("unknown", key);
    }

    [Fact]
    public void ShouldGroupIpv6AddressesByPrefix48ForBlock() {
        string? block = ClientPartition.Ipv6Block(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:dddd"));

        Assert.Equal("2001:db8:1::/48", block);
    }

    [Theory]
    [InlineData("198.51.100.7")]
    [InlineData("::ffff:198.51.100.7")]
    [InlineData(null)]
    public void ShouldHaveNoBlockForIpv4OrUnknownAddress(string? address) {
        string? block = ClientPartition.Ipv6Block(address is null ? null : IPAddress.Parse(address));

        Assert.Null(block);
    }

    [Theory]
    [InlineData("198.51.100.7", "198.51.100.0/24")]
    [InlineData("::ffff:198.51.100.7", "198.51.100.0/24")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1::/48")]
    [InlineData(null, "unknown")]
    public void ShouldGroupAddressesByNetworkForOverflow(string? address, string network) {
        string key = ClientPartition.Network(address is null ? null : IPAddress.Parse(address));

        Assert.Equal(network, key);
    }
}
