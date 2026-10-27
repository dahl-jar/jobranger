using System.Runtime.Versioning;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Matching;
using Jobranger.Cli.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using MatchingEntry = Jobranger.Cli.Features.Matching.Matching;

namespace Jobranger.Cli.Tests.Features.Matching;

[UnsupportedOSPlatform("windows")]
public sealed class GuidanceTests : IDisposable {
    private const string byteOrderMark = "\uFEFF";
    private const int limit = 12_000;

    private static readonly GuidanceText blank = new(string.Empty);
    private static readonly GuidanceText saved = new(MatchingFixtures.Guidance);

    private readonly TempDir temp = new();
    private readonly string path;

    public GuidanceTests() {
        path = Path.Combine(temp.Path, "config", "guidance.md");
    }

    public void Dispose() => temp.Dispose();

    [Fact]
    public void ShouldReturnDefault_WhenFileIsMissing() {
        GuidanceText guidance = MatchingEntry.LoadGuidance(path);

        Assert.Equal(blank, guidance);
    }

    [Fact]
    public void ShouldLoadTrimmedGuidance_WhenSavedWithBlankLines() {
        MatchingEntry.SaveGuidance(path, $"\n{MatchingFixtures.Guidance}\n\n", NullLogger.Instance);

        GuidanceText guidance = MatchingEntry.LoadGuidance(path);

        Assert.Equal(saved, guidance);
    }

    [Theory]
    [InlineData(" \n\t ")]
    [InlineData(byteOrderMark)]
    public void ShouldEmptyFile_WhenSavedTextIsBlank(string text) {
        MatchingEntry.SaveGuidance(path, MatchingFixtures.Guidance, NullLogger.Instance);

        GuidanceText result = MatchingEntry.SaveGuidance(path, text, NullLogger.Instance);

        Assert.Equal((blank, blank, string.Empty), (result, MatchingEntry.LoadGuidance(path), File.ReadAllText(path)));
    }

    [Fact]
    public void ShouldKeepSavedGuidance_WhenNewTextIsOverLimit() {
        MatchingEntry.SaveGuidance(path, MatchingFixtures.Guidance, NullLogger.Instance);

        Assert.Throws<ConfigInvalidException>(
            () => MatchingEntry.SaveGuidance(path, new string('x', limit + 1), NullLogger.Instance));

        Assert.Equal(saved, MatchingEntry.LoadGuidance(path));
    }

    [Theory]
    [InlineData(limit, false)]
    [InlineData(limit + 1, true)]
    public void ShouldRejectGuidanceFile_WhenOverLimit(int length, bool rejected) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, new string('x', length));

        Exception? error = Record.Exception(() => MatchingEntry.LoadGuidance(path));

        Assert.Equal(rejected, error is ConfigInvalidException { Errors: [{ Problem: ConfigProblem.TooLong }] });
    }

    [Theory]
    [InlineData(byteOrderMark, "\r\n")]
    [InlineData(byteOrderMark + byteOrderMark, "\r")]
    public void ShouldSavePlainLineBreaks_WhenTextHasBomAndCarriageReturns(string prefix, string newLine) {
        string text = prefix + MatchingFixtures.Guidance.Replace("\n", newLine, StringComparison.Ordinal);

        MatchingEntry.SaveGuidance(path, text, NullLogger.Instance);

        Assert.Equal(MatchingFixtures.Guidance + "\n", File.ReadAllText(path));
    }

    [Fact]
    public void ShouldRejectGuidanceFile_WhenNotUtf8() {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [(byte)'T', 0xff, (byte)'r']);

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => MatchingEntry.LoadGuidance(path));

        Assert.Equal((path, ConfigProblem.NotUtf8), (error.Path, error.Errors[0].Problem));
    }

    [Fact]
    public void ShouldRejectGuidanceFile_WhenPathIsDirectory() {
        Directory.CreateDirectory(path);

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => MatchingEntry.LoadGuidance(path));

        Assert.Equal((path, ConfigProblem.Unreadable), (error.Path, error.Errors[0].Problem));
    }

    [Fact]
    public void ShouldRejectGuidanceFile_WhenFileIsUnreadable() {
        MatchingEntry.SaveGuidance(path, MatchingFixtures.Guidance, NullLogger.Instance);
        File.SetUnixFileMode(path, UnixFileMode.None);

        ConfigInvalidException error = Assert.Throws<ConfigInvalidException>(() => MatchingEntry.LoadGuidance(path));

        Assert.Equal((path, ConfigProblem.Unreadable), (error.Path, error.Errors[0].Problem));
    }

    [Fact]
    public void ShouldWriteGuidanceFilePrivate_WhenSaved() {
        MatchingEntry.SaveGuidance(path, MatchingFixtures.Guidance, NullLogger.Instance);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void ShouldWriteGuidanceFilePrivate_WhenReset() {
        MatchingEntry.ResetGuidance(path, NullLogger.Instance);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void ShouldReturnDefault_WhenReset() {
        MatchingEntry.SaveGuidance(path, MatchingFixtures.Guidance, NullLogger.Instance);

        MatchingEntry.ResetGuidance(path, NullLogger.Instance);

        Assert.Equal((blank, string.Empty), (MatchingEntry.LoadGuidance(path), File.ReadAllText(path)));
    }
}
