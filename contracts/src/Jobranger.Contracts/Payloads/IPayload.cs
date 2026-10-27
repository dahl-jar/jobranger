namespace Jobranger.Contracts.Payloads;

public interface IPayload {
    string Type { get; }

    int V { get; }
}
