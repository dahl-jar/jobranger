using System.Text;
using Jobranger.Cli.Features.Sessions;

namespace Jobranger.Cli.Tests.Features.Sessions;

public sealed class SasWordsTests {
    private const int pinnedWordCount = 1296;

    [Fact]
    public void ShouldReadPinnedList_WhenHashMatches() {
        string pinned = string.Join('\n', SasWords.List) + "\n";
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(pinned));

        IReadOnlyList<string> words = SasWords.Read(stream);

        Assert.Equal(pinnedWordCount, words.Count);
    }

    [Fact]
    public void ShouldRefuseWordList_WhenHashDiffers() {
        string edited = string.Join('\n', SasWords.List.Select(word => word == "gecko" ? "gekko" : word)) + "\n";
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(edited));

        Assert.Throws<InvalidOperationException>(() => SasWords.Read(stream));
    }
}
