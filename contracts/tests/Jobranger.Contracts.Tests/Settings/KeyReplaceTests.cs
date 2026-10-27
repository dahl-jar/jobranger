using Jobranger.Contracts.Payloads;
using Jobranger.Contracts.Settings;
using Jobranger.Contracts.Tests.Payloads;

namespace Jobranger.Contracts.Tests.Settings;

public sealed class KeyReplaceTests {
    [Fact]
    public void ShouldHideKey_WhenRecordIsPrinted() {
        byte[] golden = PayloadFixtures.Golden("key.replace.json");
        KeyReplace replace = PayloadCodec.Decode<KeyReplace>(golden);

        string printed = replace.ToString();

        Assert.Equal("KeyReplace { Id = 00000000-0000-4000-8000-000000000011, Provider = anthropic }", printed);
    }
}
