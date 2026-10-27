using Jobranger.Cli.Files;

namespace Jobranger.Cli.Tests.Files;

public sealed class PathsTests(TestIsolation isolation) {
    private const string home = "/home/user";

    [Theory]
    [InlineData("JOBRANGER_CONFIG_DIR", "XDG_CONFIG_HOME", "config")]
    [InlineData("JOBRANGER_DATA_DIR", "XDG_DATA_HOME", "data")]
    [InlineData("JOBRANGER_STATE_DIR", "XDG_STATE_HOME", "state")]
    public void ShouldPreferProductDirEnv_WhenXdgIsAlsoSet(string productVariable, string xdgVariable, string kind) {
        Paths paths = new(new Dictionary<string, string> {
            ["HOME"] = home,
            [productVariable] = "/own",
            [xdgVariable] = "/xdg",
        });

        string dir = Dir(paths, kind);

        Assert.Equal("/own", dir);
    }

    [Theory]
    [InlineData("XDG_CONFIG_HOME", "config")]
    [InlineData("XDG_DATA_HOME", "data")]
    [InlineData("XDG_STATE_HOME", "state")]
    public void ShouldUseXdgDir_WhenProductDirEnvIsUnset(string xdgVariable, string kind) {
        Paths paths = new(new Dictionary<string, string> { ["HOME"] = home, [xdgVariable] = "/xdg" });

        string dir = Dir(paths, kind);

        Assert.Equal("/xdg/jobranger", dir);
    }

    [Fact]
    public void ShouldIgnoreProductDirEnv_WhenValueIsEmpty() {
        Paths paths = new(new Dictionary<string, string> {
            ["HOME"] = home,
            ["JOBRANGER_CONFIG_DIR"] = "",
            ["XDG_CONFIG_HOME"] = "/xdg",
        });

        string dir = paths.ConfigDir();

        Assert.Equal("/xdg/jobranger", dir);
    }

    [Fact]
    public void ShouldIgnoreXdgDir_WhenValueIsRelative() {
        Paths paths = new(new Dictionary<string, string> { ["HOME"] = home, ["XDG_CONFIG_HOME"] = "relative" });

        string dir = paths.ConfigDir();

        Assert.Equal("/home/user/.config/jobranger", dir);
    }

    [Fact]
    public void ShouldResolveProductDir_WhenValueIsRelative() {
        Paths paths = new(new Dictionary<string, string> { ["HOME"] = home, ["JOBRANGER_CONFIG_DIR"] = "cfg" });

        string dir = paths.ConfigDir();

        Assert.Equal(Path.Combine(Directory.GetCurrentDirectory(), "cfg"), dir);
    }

    [Fact]
    public void ShouldUseProcessHome_WhenHomeIsNotGiven() {
        Paths paths = new(new Dictionary<string, string>());

        string dir = paths.Home();

        Assert.Equal(Path.Combine(isolation.Root, "home"), dir);
    }

    [Fact]
    public void ShouldUseApplicationSupport_WhenOnMacOS() {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS layout");

        string[] dirs = PlatformDirs();

        Assert.Equal(
            [
                "/home/user/.config/jobranger",
                "/home/user/Library/Application Support/jobranger",
                "/home/user/Library/Application Support/jobranger",
            ],
            dirs);
    }

    [Fact]
    public void ShouldUseLocalShareAndState_WhenOnLinux() {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "Linux layout");

        string[] dirs = PlatformDirs();

        Assert.Equal(
            ["/home/user/.config/jobranger", "/home/user/.local/share/jobranger", "/home/user/.local/state/jobranger"],
            dirs);
    }

    [Fact]
    public void ShouldPlaceFilesUnderTheirDirs_WhenDirsAreSet() {
        Paths paths = new(new Dictionary<string, string> {
            ["JOBRANGER_CONFIG_DIR"] = "/config",
            ["JOBRANGER_DATA_DIR"] = "/data",
            ["JOBRANGER_STATE_DIR"] = "/state",
        });

        string[] files = [
            paths.ConfigPath(), paths.ProfilePath(), paths.ContextPath(), paths.DbPath(), paths.RunLockPath(), paths.LogDir(),
        ];

        Assert.Equal(
            [
                "/config/config.yaml", "/config/profile.yaml", "/config/context.yaml", "/data/jobranger.db",
                "/state/run.lock", "/state/logs",
            ],
            files);
    }

    private static string[] PlatformDirs() {
        Paths paths = new(new Dictionary<string, string> { ["HOME"] = home });
        return [paths.ConfigDir(), paths.DataDir(), paths.StateDir()];
    }

    private static string Dir(Paths paths, string kind) => kind switch {
        "config" => paths.ConfigDir(),
        "data" => paths.DataDir(),
        _ => paths.StateDir(),
    };
}
