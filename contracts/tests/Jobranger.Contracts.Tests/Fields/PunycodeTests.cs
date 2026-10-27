using Jobranger.Contracts.Fields;

namespace Jobranger.Contracts.Tests.Fields;

public sealed class PunycodeTests {
    public static TheoryData<string, string> Vectors => new() {
        { "他们为什么不说中文", "ihqwcrb4cv8a8dqg056pqjye" },
        { "ليهمابتكلموشعربي؟", "egbpdaj6bu4bxfgehfvwxn" },
        { "Pročprostěnemluvíčesky", "Proprostnemluvesky-uyb24dma41a" },
        { "3年B組金八先生", "3B-ww4c5e180e575a65lsy2b" },
        { "安室奈美恵-with-SUPER-MONKEYS", "-with-SUPER-MONKEYS-pc58ag80a8qai00g7n9n" },
        { "ぱふぃーでるんば", "m8jvd1abu8k6bv5c" },
        { "そのスピードで", "d9juau41awczczp" },
        { "-> $1.00 <-", "-> $1.00 <--" },
        { "a\U0001BCA0b\U000E0800", "ab-op2zi9284f" },
        { "\u4e8es\u4f44j\u4ffe", "sj-062c11ah2a" },
    };

    [Theory]
    [MemberData(nameof(Vectors))]
    public void ShouldEncodeRfcVector_WhenTextIsUnicode(string text, string encoded) {
        List<int> codePoints = [.. text.EnumerateRunes().Select(rune => rune.Value)];

        Assert.Equal(encoded, Punycode.Encode(codePoints));
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public void ShouldDecodeRfcVector_WhenTextIsPunycode(string text, string encoded) {
        Assert.Equal(text, Punycode.Decode(encoded));
    }

    [Theory]
    [InlineData("a-9")]
    [InlineData("ab_c")]
    [InlineData("a-rc4g")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzz")]
    public void ShouldReturnNull_WhenPunycodeIsInvalid(string encoded) {
        Assert.Null(Punycode.Decode(encoded));
    }
}
