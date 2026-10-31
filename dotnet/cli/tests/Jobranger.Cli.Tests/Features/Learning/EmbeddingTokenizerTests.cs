using System.IO.Compression;
using Jobranger.Cli.Features.Learning;

namespace Jobranger.Cli.Tests.Features.Learning;

public sealed class EmbeddingTokenizerTests {
    private static readonly string tokenizerPath =
        Path.Combine(AppContext.BaseDirectory, "TestData", "Embeddings", "tokenizer.json.gz");

    private static readonly Lazy<EmbeddingTokenizer> tokenizer = new(Load);

    public static TheoryData<string, int[]> Fixtures() =>
        [.. GoldenEmbeddings.Cases().Select(golden => (golden.Text, golden.Ids))];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void ShouldMatchTokenIds_WhenTextIsFixture(string text, int[] expected) {
        int[] ids = tokenizer.Value.Encode(text);

        Assert.Equal(expected, ids);
    }

    private static EmbeddingTokenizer Load() {
        using FileStream file = File.OpenRead(tokenizerPath);
        using GZipStream json = new(file, CompressionMode.Decompress);
        return new EmbeddingTokenizer(json);
    }
}
