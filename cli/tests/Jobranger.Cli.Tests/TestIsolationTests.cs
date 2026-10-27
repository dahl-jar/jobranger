using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Secrets;

namespace Jobranger.Cli.Tests;

public sealed class TestIsolationTests(TestIsolation isolation) {
    [Theory]
    [InlineData("HOME")]
    [InlineData("XDG_CONFIG_HOME")]
    [InlineData("XDG_DATA_HOME")]
    [InlineData("XDG_STATE_HOME")]
    [InlineData("XDG_CACHE_HOME")]
    [InlineData("JOBRANGER_CONFIG_DIR")]
    [InlineData("JOBRANGER_DATA_DIR")]
    [InlineData("JOBRANGER_STATE_DIR")]
    public void ShouldPointUserDirectoriesAtTempRoot_WhenAssemblyStarts(string name) {
        string? directory = Environment.GetEnvironmentVariable(name);

        Assert.NotNull(directory);
        Assert.StartsWith(isolation.Root + Path.DirectorySeparatorChar, directory, StringComparison.Ordinal);
        Assert.True(Directory.Exists(directory));
    }

    [Theory]
    [InlineData("HTTP_PROXY")]
    [InlineData("HTTPS_PROXY")]
    [InlineData("ALL_PROXY")]
    [InlineData("http_proxy")]
    [InlineData("https_proxy")]
    [InlineData("all_proxy")]
    public void ShouldClearProxy_WhenProxyIsInherited(string name) {
        string[] cleared = TestIsolation.VariablesToClear([name, "PATH"]);

        Assert.Equal([name], cleared);
    }

    [Fact]
    public void ShouldKeepOnlyAllowedSwitches_WhenProductVariablesAreInherited() {
        string[] inherited = [
            "JOBRANGER_EMBEDDINGS_LIVE", "JOBRANGER_EMBEDDINGS_CACHE", LiveSecretStores.LiveVariable,
            "JOBRANGER_INSTALL_METHOD", "JOBRANGER_CONFIG_DIR", "PATH",
        ];

        string[] cleared = TestIsolation.VariablesToClear(inherited);

        Assert.Equal(["JOBRANGER_INSTALL_METHOD", "JOBRANGER_CONFIG_DIR"], cleared);
    }

    [Fact]
    public void ShouldForceFileSecretStore_WhenAssemblyStarts() {
        string? store = Environment.GetEnvironmentVariable(SecretStores.SelectVariable);

        Assert.Equal(SecretStores.FileOnly, store);
    }
}
