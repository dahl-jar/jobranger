using System.Text;
using Jobranger.Cli.Http;
using static Jobranger.Cli.Tests.Mocks.ProviderFixtures;

namespace Jobranger.Cli.Tests.Http;

public sealed class ApiErrorsTests {
    [Theory]
    [InlineData(400, false, "BadRequest")]
    [InlineData(413, false, "BadRequest")]
    [InlineData(422, false, "BadRequest")]
    [InlineData(401, false, "Auth")]
    [InlineData(403, false, "Auth")]
    [InlineData(404, false, "NotFound")]
    [InlineData(429, false, "RateLimit")]
    [InlineData(529, true, "Overloaded")]
    [InlineData(529, false, "Server")]
    [InlineData(500, true, "Server")]
    public void ShouldMapStatusToKind_WhenApiAnswersWithError(int status, bool anthropic, string kind) {
        Assert.Equal(Enum.Parse<ApiErrorKind>(kind), ApiErrors.KindOf(status, anthropic));
    }

    [Fact]
    public void ShouldAddDetail_WhenDetailHasText() {
        Assert.Equal("HTTP 404: model not found", ApiErrors.Message(404, "model not found"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ShouldOmitDetail_WhenDetailIsBlank(string? detail) {
        Assert.Equal("HTTP 404", ApiErrors.Message(404, detail));
    }

    [Theory]
    [InlineData("{\"error\":{\"message\":\"invalid\\nmodel id\"}}", "invalid model id")]
    [InlineData("{\"message\":\"invalid model id\"}", "invalid model id")]
    [InlineData("{\"error\":{\"type\":\"invalid_request_error\"}}", null)]
    [InlineData("{\"error\":{\"message\":123}}", null)]
    [InlineData("Bad Request", null)]
    [InlineData("[1]", null)]
    [InlineData("\"text\"", null)]
    public void ShouldReadDetail_WhenBodyNamesErrorMessage(string body, string? detail) {
        Assert.Equal(detail, ApiErrors.Detail(Encoding.UTF8.GetBytes(body), AnthropicKey));
    }

    [Fact]
    public void ShouldLeaveDetailOut_WhenItShowsTheKey() {
        byte[] body = Encoding.UTF8.GetBytes("{\"error\":{\"message\":\"bad key " + AnthropicKey + "\"}}");

        Assert.Null(ApiErrors.Detail(body, AnthropicKey));
    }

    [Fact]
    public void ShouldAddDetail_WhenErrorIsBadRequest() {
        ApiException error = ApiErrors.ToException(Reply(400, null), anthropic: true, AnthropicKey);

        Assert.Equal((ApiErrorKind.BadRequest, "HTTP 400: failed"), (error.Kind, error.Message));
    }

    [Fact]
    public void ShouldLeaveDetailOut_WhenErrorIsAuth() {
        ApiException error = ApiErrors.ToException(Reply(401, null), anthropic: true, AnthropicKey);

        Assert.Equal((ApiErrorKind.Auth, "HTTP 401"), (error.Kind, error.Message));
    }

    [Fact]
    public void ShouldLeaveDetailOut_WhenHostIsNotOfficial() {
        ApiException error = ApiErrors.ToException(Reply(400, null), anthropic: false, null);

        Assert.Equal("HTTP 400", error.Message);
    }

    [Theory]
    [InlineData(429, false)]
    [InlineData(529, true)]
    public void ShouldKeepRetryAfter_WhenErrorPauses(int status, bool anthropic) {
        ApiException error = ApiErrors.ToException(Reply(status, TimeSpan.FromSeconds(30)), anthropic, AnthropicKey);

        Assert.Equal(TimeSpan.FromSeconds(30), error.RetryAfter);
    }

    [Fact]
    public void ShouldIgnoreRetryAfter_WhenStatusIsServerError() {
        ApiException error = ApiErrors.ToException(Reply(500, TimeSpan.FromSeconds(30)), anthropic: true, AnthropicKey);

        Assert.Null(error.RetryAfter);
    }

    private static ApiReply Reply(int status, TimeSpan? retryAfter) =>
        new(status, Encoding.UTF8.GetBytes(ErrorBody), retryAfter);
}
