namespace Jobranger.Cli.Http;

internal sealed record SdkRules(string TimeoutMessage, double LongestServerWaitSeconds, bool RefusesLongerWait) {
    private const string anthropicTimeout = "Request timed out or interrupted. This could be due to a network timeout, "
        + "dropped connection, or request cancellation. "
        + "See https://docs.anthropic.com/en/api/errors#long-requests for more details.";
    private const string openAITimeout = "Request timed out.";
    private const double anthropicLongestWait = 4_294_967;
    private const double openAILongestWait = 120;

    public static SdkRules Anthropic { get; } = new(anthropicTimeout, anthropicLongestWait, RefusesLongerWait: false);

    public static SdkRules OpenAI { get; } = new(openAITimeout, openAILongestWait, RefusesLongerWait: true);
}
