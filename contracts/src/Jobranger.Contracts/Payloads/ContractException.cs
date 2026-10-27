namespace Jobranger.Contracts.Payloads;

public sealed class ContractException : Exception {
    internal const string RootPath = "$";

    public ContractException(string? messageType, string path, string keyword)
        : base($"payload {path} fails {keyword}") {
        MessageType = messageType;
        Path = path;
        Keyword = keyword;
    }

    public string? MessageType { get; }

    public string Path { get; }

    public string Keyword { get; }

    internal static ContractException AtRoot(string keyword) => new(null, RootPath, keyword);
}
