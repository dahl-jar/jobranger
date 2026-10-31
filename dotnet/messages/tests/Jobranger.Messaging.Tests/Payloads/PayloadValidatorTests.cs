using System.Text.Json;
using Jobranger.Messaging.Payloads;

namespace Jobranger.Messaging.Tests.Payloads;

public sealed class PayloadValidatorTests {
    private const string docIdDefinition = "DocId";
    private const int oversizeChars = 65;

    [Fact]
    public void ShouldNameMaxLength_WhenOversizeTextAlsoBreaksPattern() {
        JsonElement instance = JsonSerializer.SerializeToElement(new string('a', oversizeChars), MessagesJsonContext.Default.String);

        ContractException error = Assert.Throws<ContractException>(() => PayloadValidator.Validate(instance, docIdDefinition, null));

        Assert.Equal("maxLength", error.Keyword);
    }
}
