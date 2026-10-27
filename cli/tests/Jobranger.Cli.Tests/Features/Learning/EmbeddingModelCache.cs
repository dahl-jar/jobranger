using Jobranger.Cli.Features.Learning;

namespace Jobranger.Cli.Tests.Features.Learning;

internal static class EmbeddingModelCache {
    public const string CacheVariable = "JOBRANGER_EMBEDDINGS_CACHE";
    public const string LiveVariable = "JOBRANGER_EMBEDDINGS_LIVE";
    public const string MissingReason =
        "model files are not cached: set " + CacheVariable + " to a folder holding "
        + EmbeddingModel.ModelFileName + " and " + EmbeddingModel.TokenizerFileName;
    public const string OfflineReason = "live download is off: set " + LiveVariable + "=1 to download the model";

    private const string liveValue = "1";

    public static bool IsLive => Environment.GetEnvironmentVariable(LiveVariable) == liveValue;

    public static string? Folder => Environment.GetEnvironmentVariable(CacheVariable);

    public static bool HasModel =>
        Folder is string folder && EmbeddingModel.Files.All(file => File.Exists(Path.Combine(folder, file.Name)));

    public static void LinkInto(DirectoryInfo target) {
        foreach (ModelFile file in EmbeddingModel.Files) {
            File.CreateSymbolicLink(Path.Combine(target.FullName, file.Name), Path.Combine(Folder!, file.Name));
        }
    }
}
