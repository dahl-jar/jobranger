using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Jobranger.Relay.Tests.Composition;

public sealed class StartupTests {
    [Fact]
    public void ShouldRefuseToStartWithoutHandleKey() {
        using RelayFactory factory = new();
        using WebApplicationFactory<Program> missingKey = factory.WithWebHostBuilder(builder => builder.UseSetting("Relay:HandleKey", ""));

        Assert.Throws<InvalidOperationException>(() => missingKey.CreateClient());
    }
}
