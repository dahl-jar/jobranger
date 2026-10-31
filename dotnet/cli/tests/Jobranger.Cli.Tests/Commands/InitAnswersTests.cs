using System.Runtime.Versioning;
using System.Text;
using Jobranger.Cli.Config;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using CatalogueEntry = Jobranger.Cli.Catalogue.Catalogue;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class InitAnswersTests : IDisposable {
    private static readonly EnglishMessages english = new();

    private readonly InitHarness init = new();

    private object[] BeforeDelivery => [.. InitHarness.ModelAnswers, .. init.YouAnswers, .. InitHarness.LimitAnswers];

    private object[] EmailOnly => [.. BeforeDelivery, new[] { InitFixtures.Email }];

    public void Dispose() => init.Dispose();

    [Fact]
    public async Task ShouldAskAgain_WhenProfileIsMissing() {
        string missing = init.TempFile("missing.yaml");

        int exit = await RetryProfileAsync(missing);

        Assert.Equal((CliExitCodes.Ok, init.Profile), (exit, init.Saved.Profile.Path));
        Assert.Contains(english.InitProfileMissing(missing), init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenProfileIsInvalid() {
        string invalid = init.TempFile("invalid.yaml");
        File.WriteAllText(invalid, "professional_years: 1\n");

        int exit = await RetryProfileAsync(invalid);

        Assert.Equal((CliExitCodes.Ok, init.Profile), (exit, init.Saved.Profile.Path));
        Assert.Contains($"summary: {english.ConfigRequired}", init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldExpandHome_WhenProfilePathStartsWithTilde() {
        ScriptedPrompter prompter = init.Prompter(
            [
                .. InitHarness.ModelAnswers, "~/profile.yaml", new[] { InitFixtures.Role }, .. InitHarness.LimitAnswers,
                .. InitHarness.WebsiteAnswers,
            ]);

        int exit = await init.InvokeAsync(init.Seams(prompter, InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, init.Profile), (exit, init.Saved.Profile.Path));
        Assert.Contains((english.InitProfilePath, "~/config/profile.yaml"), prompter.TextDefaults);
    }

    [Fact]
    public async Task ShouldOfferEveryCatalogueRole() {
        ScriptedPrompter prompter = init.Prompter(init.WebsiteFlow);

        await init.InvokeAsync(init.Seams(prompter, InitHarness.Listing()));

        Assert.Equal(
            CatalogueEntry.Load().RoleList().Select(role => role.Id),
            prompter.Choices[2].Select(choice => choice.Value));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(11)]
    public async Task ShouldAskAgain_WhenNothingIsPicked(int position) {
        List<object> answers = [.. init.WebsiteFlow];
        answers.Insert(position, Array.Empty<string>());

        int exit = await init.InvokeAsync(init.Seams(init.Prompter([.. answers]), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(english.InitPickOne, init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldSaveLimits_WhenAnswered() {
        object[] answers = [
            .. InitHarness.ModelAnswers, .. init.YouAnswers, new[] { "senior" }, new[] { "6+" }, false, string.Empty, false,
            .. InitHarness.WebsiteAnswers,
        ];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Limits saved = init.Saved.Targets.Limits;
        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Equal(["senior"], saved.Levels);
        Assert.Equal(["6+"], saved.Years);
        Assert.False(saved.HideMasters);
    }

    [Theory]
    [InlineData(true, "  Bergen, Oslo, open to moving")]
    [InlineData(false, "  Bergen, Oslo")]
    public async Task ShouldAskRelocate_WhenPlacesAreGiven(bool relocate, string line) {
        object[] answers = [
            .. InitHarness.ModelAnswers, .. init.YouAnswers, InitFixtures.DefaultLevels, InitFixtures.DefaultYears, true,
            "Bergen, ,Oslo", relocate, false, .. InitHarness.WebsiteAnswers,
        ];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Targets saved = init.Saved.Targets;
        Assert.Equal((CliExitCodes.Ok, relocate), (exit, saved.Relocate));
        Assert.Equal(["Bergen", "Oslo"], saved.Places);
        Assert.Contains(InitHarness.Lines(line), init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldSaveEmployerFeeds_WhenFlowEnds() {
        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.WebsiteFlow), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Equal(CatalogueEntry.Load().DefaultEmployerFeeds(), init.Saved.Sources.EmployerFeeds);
    }

    [Fact]
    public async Task ShouldLeaveFinnOff_WhenUserSkipsOptIn() {
        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.WebsiteFlow), InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, false, false), (exit, init.Saved.Sources.Finn, init.Saved.Sources.LinkedIn));
    }

    [Theory]
    [InlineData(ConfigKeys.Finn, true, false)]
    [InlineData(ConfigKeys.LinkedIn, false, true)]
    public async Task ShouldTurnOnSource_WhenUserOptsIn(string picked, bool finn, bool linkedIn) {
        object[] answers = [
            .. InitHarness.ModelAnswers, .. init.YouAnswers, InitFixtures.DefaultLevels, InitFixtures.DefaultYears, true,
            string.Empty, true, new[] { picked }, .. InitHarness.WebsiteAnswers,
        ];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, finn, linkedIn), (exit, init.Saved.Sources.Finn, init.Saved.Sources.LinkedIn));
    }

    [Fact]
    public async Task ShouldNotStoreSecret_WhenTestEmailFails() {
        await using FakeSmtpServer refusing = new(async session => {
            await FakeSmtpServer.ReachStartTlsAsync(session);
            await session.UpgradeAsync();
            await FakeSmtpServer.HelloAsync(session, InitFixtures.AuthPlain);
            await session.ReadLineAsync();
            await session.SendAsync(InitFixtures.LoginRefused);
            await session.StallAsync();
        });
        await using FakeSmtpServer accepting = InitHarness.AcceptingServer();
        ScriptedPrompter prompter = init.Prompter([
            .. EmailOnly, DeliveryFixtures.Address,
            InitFixtures.Smtp, InitHarness.ServerAnswer(refusing), string.Empty, ConfigFixtures.Secret,
            InitFixtures.Smtp, InitHarness.ServerAnswer(accepting), string.Empty, InitFixtures.OtherPassword,
            string.Empty,
        ]);

        List<string?> passwords = [];
        init.Store.AfterWrite = () => passwords.Add(init.Store.Values.GetValueOrDefault(Credentials.SmtpPasswordName));

        int exit = await init.InvokeAsync(init.Seams(prompter, InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains($"✕ SMTP login to 127.0.0.1:{refusing.Port} failed.", init.Output, StringComparison.Ordinal);
        Assert.Equal(2, prompter.Asked.Count(message => message == english.InitSendThrough));
        Assert.Equal([null, InitFixtures.OtherPassword], passwords);
        Assert.Equal(
            [string.Empty, InitHarness.ServerAnswer(refusing)],
            prompter.TextDefaults.Where(asked => asked.Message == english.InitSmtpServer).Select(asked => asked.Default));
    }

    [Fact]
    public async Task ShouldSuggestGmailServer_WhenAddressIsGmail() {
        ScriptedPrompter prompter = init.Prompter([.. EmailOnly, "user@gmail.com", InitFixtures.Smtp, ScriptedPrompter.Cancel]);

        await init.InvokeAsync(init.Seams(prompter, InitHarness.Listing()));

        Assert.Equal((english.InitSmtpServer, "smtp.gmail.com:465"), prompter.TextDefaults[^1]);
    }

    [Fact]
    public async Task ShouldSendTestEmail_WhenTransportIsSmtp() {
        await using FakeSmtpServer server = InitHarness.AcceptingServer();

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.SmtpFlow(server)), InitHarness.Listing()));

        EmailConfig saved = init.Saved.Notify.Email;
        Assert.Equal(
            (CliExitCodes.Ok, server.Url(InitFixtures.Smtp), DeliveryFixtures.Address, true),
            (exit, saved.SmtpUrl, saved.SmtpUser, saved.On));
        Assert.Contains(ConfigFixtures.Secret, server.DecodedLogin(), StringComparison.Ordinal);
        Assert.Contains("jobranger test email", Encoding.Latin1.GetString(Assert.Single(server.Messages)), StringComparison.Ordinal);
        Assert.Contains(english.InitTestSent(DeliveryFixtures.Address), init.Output, StringComparison.Ordinal);
        Assert.Equal(ConfigFixtures.Secret, init.Store.Values[Credentials.SmtpPasswordName]);
        string place = OperatingSystem.IsMacOS() ? english.InitStoreKeychain : english.InitStoreKeyring;
        Assert.Contains(english.InitSecretsStored(place), init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldSendTestEmail_WhenTransportIsResend() {
        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.ResendFlow()), InitHarness.Listing()));

        EmailConfig saved = init.Saved.Notify.Email;
        Assert.Equal(
            (CliExitCodes.Ok, $"Bearer {DeliveryFixtures.ResendKey}", MailAddresses.DefaultResendSender, EmailVia.Resend),
            (exit, Assert.Single(init.ResendHandler.Requests).Headers["Authorization"], saved.Sender, saved.Via));
        Assert.Equal(DeliveryFixtures.ResendKey, init.Store.Values[Credentials.ResendKeyName]);
    }

    [Fact]
    public async Task ShouldSkipEmail_WhenOnlyWebsiteIsPicked() {
        ScriptedPrompter prompter = init.Prompter(init.WebsiteFlow);

        int exit = await init.InvokeAsync(init.Seams(prompter, InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, false, true), (exit, init.Saved.Notify.Email.On, init.Saved.Notify.Website));
        Assert.DoesNotContain(english.InitAddress, prompter.Asked);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenAddressIsInvalid() {
        object[] answers = [
            .. EmailOnly, "not-an-address", DeliveryFixtures.Address, InitFixtures.Resend, DeliveryFixtures.ResendKey,
            string.Empty, string.Empty,
        ];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, DeliveryFixtures.Address), (exit, init.Saved.Notify.Email.Address));
        Assert.Contains(english.InitNotAddress, init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenServerIsInvalid() {
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        object[] answers = [
            .. EmailOnly, DeliveryFixtures.Address, InitFixtures.Smtp, "mail example", InitHarness.ServerAnswer(server),
            string.Empty, ConfigFixtures.Secret, string.Empty,
        ];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, server.Url(InitFixtures.Smtp)), (exit, init.Saved.Notify.Email.SmtpUrl));
        Assert.Contains(english.InitNotSmtpServer, init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenSenderIsInvalid() {
        object[] answers = [
            .. EmailOnly, DeliveryFixtures.Address, InitFixtures.Resend, DeliveryFixtures.ResendKey, "not a sender",
            DeliveryFixtures.Sender, string.Empty,
        ];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, DeliveryFixtures.Sender), (exit, init.Saved.Notify.Email.Sender));
        Assert.Contains(english.InitNotSender, init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenRunTimesAreTooClose() {
        object[] answers = [.. init.WebsiteFlow[..^1], "23:00, 00:30", "3:00, 15:00"];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(
            english.InitRunTimesWrong($"schedule.times: {english.ConfigRunTimesTooClose(2)}"), init.Output, StringComparison.Ordinal);
        Assert.Equal(["03:00", "15:00"], init.Saved.Schedule.Times);
    }

    [Fact]
    public async Task ShouldAskAgain_WhenRunTimesAreEmpty() {
        object[] answers = [.. init.WebsiteFlow[..^1], ", ,", "12:00"];

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Contains(english.InitRunTimesWrong(english.ConfigRunTimeFormat), init.Output, StringComparison.Ordinal);
        Assert.Equal(["12:00"], init.Saved.Schedule.Times);
    }

    private Task<int> RetryProfileAsync(string badPath) {
        object[] answers = [.. InitHarness.ModelAnswers, badPath, .. init.AfterModel];
        return init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));
    }
}
