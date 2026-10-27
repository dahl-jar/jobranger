namespace Jobranger.Cli.Features.Learning;

internal sealed class ModelHashException : Exception {
    public ModelHashException(string fileName, string expected, string actual)
        : base($"{fileName} sha256 {actual} differs from pinned {expected}, file deleted") {
    }

    public ModelHashException(string fileName, string expected, string actual, Exception deleteFailure)
        : base($"{fileName} sha256 {actual} differs from pinned {expected}, file could not be deleted", deleteFailure) {
    }
}
