namespace Jobranger.Cli.Features.Link;

internal sealed class RelayRejectedException(int status, string code)
    : Exception($"relay refused the request status={status} code={code}") {
    public int Status { get; } = status;

    public string Code { get; } = code;
}
