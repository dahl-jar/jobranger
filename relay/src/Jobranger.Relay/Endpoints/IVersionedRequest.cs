namespace Jobranger.Relay.Endpoints;

internal interface IVersionedRequest {
    int? V { get; }
}
