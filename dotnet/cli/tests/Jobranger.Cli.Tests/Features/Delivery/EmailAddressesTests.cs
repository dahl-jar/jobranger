using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Delivery;

public sealed class EmailAddressesTests {
    [Theory]
    [InlineData("Owner <sender@example.test>", nameof(EmailVia.Smtp), "Owner <sender@example.test>")]
    [InlineData(null, nameof(EmailVia.Resend), "jobranger <onboarding@resend.dev>")]
    [InlineData(null, nameof(EmailVia.Smtp), "jobranger <user@example.test>")]
    public void ShouldPickSender_WhenConfigIsGiven(string? sender, string via, string expected) {
        EmailConfig email = new() { Address = DeliveryFixtures.Address, Sender = sender, Via = Enum.Parse<EmailVia>(via) };

        EmailAddresses? addresses = EmailAddresses.From(email);

        Assert.Equal(expected, addresses?.Sender);
    }

    [Fact]
    public void ShouldRefuseAddresses_WhenTheyAreUnusable() {
        EmailConfig email = new() { Address = "not an address", Sender = DeliveryFixtures.Sender };

        EmailAddresses? addresses = EmailAddresses.From(email);

        Assert.Null(addresses);
    }
}
