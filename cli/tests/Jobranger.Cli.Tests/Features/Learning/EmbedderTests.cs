using System.Runtime.Versioning;
using Jobranger.Cli.Features.Learning;

namespace Jobranger.Cli.Tests.Features.Learning;

public sealed class EmbedderTests : IDisposable {
    private const float tolerance = 1e-5f;
    private const UnixFileMode readOnlyFolder = UnixFileMode.UserRead | UnixFileMode.UserExecute;

    private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("jobranger-embedder-");

    public void Dispose() {
        if (!OperatingSystem.IsWindows()) {
            File.SetUnixFileMode(folder.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        folder.Delete(recursive: true);
    }

    [Fact]
    public void ShouldMatchGoldenEmbeddings_WhenTextIsFixture() {
        Assert.SkipUnless(EmbeddingModelCache.HasModel, EmbeddingModelCache.MissingReason);
        EmbeddingModelCache.LinkInto(folder);
        using Embedder embedder = new(folder.FullName);

        string[] failures = Differences(embedder);

        Assert.Empty(failures);
    }

    [Fact]
    public void ShouldReturnNoVectors_WhenTextsAreEmpty() {
        Assert.SkipUnless(EmbeddingModelCache.HasModel, EmbeddingModelCache.MissingReason);
        EmbeddingModelCache.LinkInto(folder);
        using Embedder embedder = new(folder.FullName);

        float[][] vectors = embedder.Embed([]);

        Assert.Empty(vectors);
    }

    [Fact]
    public async Task ShouldMatchGoldenEmbeddings_WhenModelIsDownloaded() {
        Assert.SkipUnless(EmbeddingModelCache.IsLive, EmbeddingModelCache.OfflineReason);
        using HttpClient http = new();
        ModelDownloader downloader = new(http, folder.FullName, EmbeddingModel.Files);
        await downloader.EnsureAsync(TestContext.Current.CancellationToken);
        using Embedder embedder = new(folder.FullName);

        string[] failures = Differences(embedder);

        Assert.Empty(failures);
    }

    [Fact]
    public void ShouldMatchGoldenFiles_WhenModelFilesArePinned() {
        IReadOnlyList<ModelFile> golden = GoldenEmbeddings.Files();

        Assert.Equal(golden, EmbeddingModel.Files);
    }

    [Fact]
    public void ShouldDeleteModel_WhenHashDiffers() {
        string model = Path.Combine(folder.FullName, EmbeddingModel.ModelFileName);
        File.WriteAllText(model, "not a model");
        File.WriteAllText(Path.Combine(folder.FullName, EmbeddingModel.TokenizerFileName), "{}");

        Assert.Throws<ModelHashException>(() => new Embedder(folder.FullName));

        Assert.False(File.Exists(model));
    }

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void ShouldKeepHashError_WhenModelCannotBeDeleted() {
        if (OperatingSystem.IsWindows()) {
            Assert.Skip("unix file modes only");
        }
        File.WriteAllText(Path.Combine(folder.FullName, EmbeddingModel.ModelFileName), "not a model");
        File.WriteAllText(Path.Combine(folder.FullName, EmbeddingModel.TokenizerFileName), "{}");
        File.SetUnixFileMode(folder.FullName, readOnlyFolder);

        ModelHashException error = Assert.Throws<ModelHashException>(() => new Embedder(folder.FullName));

        Assert.IsType<UnauthorizedAccessException>(error.InnerException);
    }

    private static string[] Differences(Embedder embedder) {
        IReadOnlyList<GoldenCase> cases = GoldenEmbeddings.Cases();
        float[][] vectors = embedder.Embed([.. cases.Select(golden => golden.Text)]);
        return [.. cases.Zip(vectors)
            .Select(pair => (pair.First.Text, Difference: MaxDifference(pair.First.Embedding, pair.Second)))
            .Where(result => result.Difference > tolerance)
            .Select(result => $"{result.Text}: {result.Difference}")];
    }

    private static float MaxDifference(float[] expected, float[] actual) =>
        expected.Length != actual.Length
            ? float.PositiveInfinity
            : expected.Zip(actual).Max(pair => Distance(pair.First, pair.Second));

    private static float Distance(float expected, float actual) =>
        float.IsNaN(actual) ? float.PositiveInfinity : Math.Abs(expected - actual);
}
