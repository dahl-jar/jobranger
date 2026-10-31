namespace Jobranger.Messaging.Payloads;

public interface IPayload {
    string Type { get; }

    int V { get; }
}
