using Microsoft.ML.Tokenizers;

namespace Jobranger.Cli.Features.Learning;

internal sealed class EmbeddingTokenizer {
    private const int maxTokens = 128;
    private const int specialTokenCount = 2;

    private readonly SentencePieceTokenizer tokenizer;

    public EmbeddingTokenizer(Stream json) {
        tokenizer = SentencePieceTokenizer.CreateFromTokenizerJson(json);
    }

    public int[] Encode(string text) {
        IReadOnlyList<int> pieces = tokenizer.EncodeToIds(
            text, addBeginningOfSentence: false, addEndOfSentence: false);
        return [
            tokenizer.BeginningOfSentenceId,
            .. pieces.Take(maxTokens - specialTokenCount),
            tokenizer.EndOfSentenceId,
        ];
    }
}
