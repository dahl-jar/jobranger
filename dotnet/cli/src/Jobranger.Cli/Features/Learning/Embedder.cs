using System.Numerics.Tensors;
using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;

namespace Jobranger.Cli.Features.Learning;

internal sealed class Embedder : IDisposable {
    private const int batchSize = 16;

    private static readonly string[] inputNames = ["input_ids", "attention_mask", "token_type_ids"];
    private static readonly string[] outputNames = ["last_hidden_state"];

    private readonly EmbeddingTokenizer tokenizer;
    private readonly InferenceSession session;

    public Embedder(string modelDir) {
        foreach (ModelFile file in EmbeddingModel.Files) {
            VerifyOrDelete(Path.Combine(modelDir, file.Name), file);
        }
        using (FileStream json = File.OpenRead(Path.Combine(modelDir, EmbeddingModel.TokenizerFileName))) {
            tokenizer = new EmbeddingTokenizer(json);
        }
        session = new InferenceSession(Path.Combine(modelDir, EmbeddingModel.ModelFileName));
    }

    public float[][] Embed(IReadOnlyList<string> texts) {
        float[][] vectors = new float[texts.Count][];
        for (int start = 0; start < texts.Count; start += batchSize) {
            int[][] rows = [.. texts.Skip(start).Take(batchSize).Select(tokenizer.Encode)];
            EmbedBatch(rows).CopyTo(vectors, start);
        }
        return vectors;
    }

    public void Dispose() => session.Dispose();

    private static void VerifyOrDelete(string path, ModelFile file) {
        string actual;
        using (FileStream stream = File.OpenRead(path)) {
            actual = Convert.ToHexStringLower(SHA256.HashData(stream));
        }
        if (actual == file.Sha256) {
            return;
        }
        try {
            File.Delete(path);
        } catch (Exception deleteFailure) when (deleteFailure is IOException or UnauthorizedAccessException) {
            throw new ModelHashException(file.Name, file.Sha256, actual, deleteFailure);
        }
        throw new ModelHashException(file.Name, file.Sha256, actual);
    }

    private float[][] EmbedBatch(int[][] rows) {
        int width = rows.Max(row => row.Length);
        long[] shape = [rows.Length, width];
        long[] ids = new long[rows.Length * width];
        long[] mask = new long[ids.Length];
        long[] types = new long[ids.Length];
        for (int row = 0; row < rows.Length; row++) {
            for (int token = 0; token < rows[row].Length; token++) {
                ids[(row * width) + token] = rows[row][token];
                mask[(row * width) + token] = 1;
            }
        }
        using OrtValue idsValue = OrtValue.CreateTensorValueFromMemory(ids, shape);
        using OrtValue maskValue = OrtValue.CreateTensorValueFromMemory(mask, shape);
        using OrtValue typesValue = OrtValue.CreateTensorValueFromMemory(types, shape);
        using RunOptions options = new();
        using IDisposableReadOnlyCollection<OrtValue> outputs =
            session.Run(options, inputNames, [idsValue, maskValue, typesValue], outputNames);
        ReadOnlySpan<float> hidden = outputs[0].GetTensorDataAsSpan<float>();
        int rowSize = width * EmbeddingModel.Dimension;
        float[][] vectors = new float[rows.Length][];
        for (int row = 0; row < rows.Length; row++) {
            vectors[row] = Pool(hidden.Slice(row * rowSize, rowSize), rows[row].Length);
        }
        return vectors;
    }

    private static float[] Pool(ReadOnlySpan<float> hidden, int tokens) {
        float[] vector = new float[EmbeddingModel.Dimension];
        for (int token = 0; token < tokens; token++) {
            TensorPrimitives.Add(
                vector, hidden.Slice(token * EmbeddingModel.Dimension, EmbeddingModel.Dimension), vector);
        }
        TensorPrimitives.Divide(vector, TensorPrimitives.Norm<float>(vector), vector);
        return vector;
    }
}
