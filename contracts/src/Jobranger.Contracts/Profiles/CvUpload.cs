using Jobranger.Contracts.Payloads;

namespace Jobranger.Contracts.Profiles;

public sealed record CvUpload(string Id, string Name, int SizeBytes, string ContentB64) : PayloadBase(Tag) {
    public const string Tag = "cv.upload";
}
