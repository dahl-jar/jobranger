namespace Jobranger.Cli.Http;

internal sealed record SdkRules(string TimeoutMessage, double? RefusedWaitSeconds) {
    private const string anthropicTimeout = "Request timed out or interrupted. This could be due to a network timeout, "
        + "dropped connection, or request cancellation. "
        + "See https://docs.anthropic.com/en/api/errors#long-requests for more details.";
    private const string openAITimeout = "Request timed out.";
    private const double openAIRefusedWait = 120;

    public static SdkRules Anthropic { get; } = new(anthropicTimeout, RefusedWaitSeconds: null);

    public static SdkRules OpenAI { get; } = new(openAITimeout, openAIRefusedWait);
}
