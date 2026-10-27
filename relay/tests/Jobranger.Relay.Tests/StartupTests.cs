using Microsoft.AspNetCore.Hosting;

namespace Jobranger.Relay.Tests;

public sealed class StartupTests
{
    [Fact]
    public void ShouldRefuseToStartWithoutHandleKey()
    {
        using RelayFactory factory = new();
        using var missingKey = factory.WithWebHostBuilder(builder => builder.UseSetting("Relay:HandleKey", ""));

        Assert.Throws<InvalidOperationException>(() => missingKey.CreateClient());
    }
}
