using Jobranger.Cli.Http;

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
    [InlineData(418, true, "Server")]
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
}
