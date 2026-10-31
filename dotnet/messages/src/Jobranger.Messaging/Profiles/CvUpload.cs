using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Profiles;

public sealed record CvUpload(string Id, string Name, int SizeBytes, string ContentB64) : PayloadBase(Tag) {
    public const string Tag = "cv.upload";
}
