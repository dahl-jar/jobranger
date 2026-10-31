using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Delivery;
using Jobranger.Cli.Tests.Mocks;

namespace Jobranger.Cli.Tests.Features.Delivery;

public sealed class EmailAddressesTests {
    [Theory]
    [InlineData("Owner <sender@example.test>")]
    [InlineData("sender@example.test")]
    public void ShouldKeepConfiguredSender_WhenConfigHasOne(string sender) {
        EmailConfig email = new() { Address = DeliveryFixtures.Address, Sender = sender, Via = EmailVia.Smtp };

        EmailAddresses? addresses = EmailAddresses.From(email);

        Assert.Equal(sender, addresses?.Sender);
    }

    [Theory]
    [InlineData(null, nameof(EmailVia.Resend), "jobranger <onboarding@resend.dev>")]
    [InlineData(null, nameof(EmailVia.Smtp), "jobranger <user@example.test>")]
    [InlineData("", nameof(EmailVia.Smtp), "jobranger <user@example.test>")]
    public void ShouldFallBackToDefaultSender_WhenConfigHasNone(string? sender, string via, string expected) {
        EmailConfig email = new() { Address = DeliveryFixtures.Address, Sender = sender, Via = Enum.Parse<EmailVia>(via) };

        EmailAddresses? addresses = EmailAddresses.From(email);

        Assert.Equal(expected, addresses?.Sender);
    }

    [Fact]
    public void ShouldRefuseAddresses_WhenAddressIsUnusable() {
        EmailConfig email = new() { Address = "not an address", Sender = DeliveryFixtures.Sender };

        EmailAddresses? addresses = EmailAddresses.From(email);

        Assert.Null(addresses);
    }

    [Theory]
    [InlineData("not a mailbox")]
    [InlineData("job\r\nranger <sender@example.test>")]
    public void ShouldRefuseAddresses_WhenSenderIsUnusable(string sender) {
        EmailConfig email = new() { Address = DeliveryFixtures.Address, Sender = sender };

        EmailAddresses? addresses = EmailAddresses.From(email);

        Assert.Null(addresses);
    }
}
