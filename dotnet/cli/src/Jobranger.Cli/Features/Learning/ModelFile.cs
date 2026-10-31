namespace Jobranger.Cli.Features.Learning;

internal sealed record ModelFile(string Name, Uri Url, string Sha256, long Size) {
    public void CheckHash(string actual) {
        if (actual != Sha256) {
            throw new ModelHashException(Name, Sha256, actual);
        }
    }
}
