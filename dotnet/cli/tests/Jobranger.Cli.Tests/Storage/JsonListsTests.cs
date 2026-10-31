using Jobranger.Cli.Storage;

namespace Jobranger.Cli.Tests.Storage;

public sealed class JsonListsTests {
    [Theory]
    [InlineData(new string[0], "[]")]
    [InlineData(new[] { "Søk", "Java" }, "[\"Søk\", \"Java\"]")]
    [InlineData(new[] { "C\"#", "back\\slash" }, "[\"C\\\"#\", \"back\\\\slash\"]")]
    [InlineData(new[] { "line\nbreak\ttab\u0001" }, "[\"line\\nbreak\\ttab\\u0001\"]")]
    [InlineData(new[] { "a b\r\b\f" }, "[\"a b\\r\\b\\f\"]")]
    public void ShouldWritePythonJson_WhenListIsStored(string[] items, string expected) {
        Assert.Equal(expected, JsonLists.Write(items));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not json")]
    [InlineData("{\"a\": 1}")]
    [InlineData("[\"a\\ud800\"]")]
    public void ShouldReadEmptyList_WhenStoredValueIsNotJsonList(string? stored) {
        Assert.Empty(JsonLists.Read(stored));
    }

    [Fact]
    public void ShouldReadItemsAsText_WhenStoredListHoldsNumbers() {
        Assert.Equal(["Java", "5"], JsonLists.Read("[\"Java\", 5]"));
    }
}
