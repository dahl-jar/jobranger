namespace Jobranger.Cli.Features.Learning;

internal static class EmbeddingModel {
    public const string ModelFileName = "model.onnx";
    public const string TokenizerFileName = "tokenizer.json";
    public const int Dimension = 384;

    private const string baseUrl =
        "https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2/resolve/"
        + "e8f8c211226b894fcb81acc59f3b34ba3efd5f42/";

    public static IReadOnlyList<ModelFile> Files { get; } = [
        new(
            ModelFileName,
            new Uri(baseUrl + "onnx/model.onnx"),
            "10f7a088420252b26caf819236ca2c9d2987afd0fc06fec7553b542a5655a05a",
            470_301_610),
        new(
            TokenizerFileName,
            new Uri(baseUrl + "tokenizer.json"),
            "2c3387be76557bd40970cec13153b3bbf80407865484b209e655e5e4729076b8",
            9_081_518),
    ];
}
