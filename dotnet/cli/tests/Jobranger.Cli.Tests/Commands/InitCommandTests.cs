using System.Runtime.Versioning;
using Jobranger.Cli.Commands;
using Jobranger.Cli.Config;
using Jobranger.Cli.Features.Setup;
using Jobranger.Cli.Secrets;
using Jobranger.Cli.Tests.Mocks;
using Jobranger.Cli.Text;
using SetupEntry = Jobranger.Cli.Features.Setup.Setup;

namespace Jobranger.Cli.Tests.Commands;

[UnsupportedOSPlatform("windows")]
public sealed class InitCommandTests : IDisposable {
    private const string homeMark = "{home}";
    private const string corruptSecrets = "{not json";
    private const string validSecrets = "{}";

    private static readonly EnglishMessages english = new();

    private readonly InitHarness init = new();

    public void Dispose() => init.Dispose();

    [Theory]
    [InlineData(Locales.English, "jobranger init needs a terminal. Run it in a terminal window.")]
    [InlineData(Locales.Norwegian, "jobranger init trenger en terminal. Kjør kommandoen i et terminalvindu.")]
    public async Task ShouldPrintOneLine_WhenInputIsRedirected(string language, string line) {
        init.SetLanguage(language);
        ScriptedPrompter prompter = init.Prompter();

        int exit = await init.InvokeAsync(init.Seams(prompter), inputRedirected: true);

        Assert.Equal((CliExitCodes.Failure, InitHarness.Lines(line), 0), (exit, init.Errors, prompter.Asked.Count));
        Assert.Empty(init.Output);
        Assert.Equal("init.failed not_a_terminal", CommandFixtures.LoggedFailure(init.Paths));
    }

    [Fact]
    public async Task ShouldPrintOneLine_WhenPrompterIsUnavailable() {
        int exit = await init.InvokeAsync(init.Seams(null));

        Assert.Equal((CliExitCodes.Failure, InitHarness.Lines(english.InitNeedsTerminal)), (exit, init.Errors));
    }

    [Fact]
    public async Task ShouldKeepConfig_WhenUserDeclinesStartOver() {
        byte[] old = init.WriteOldConfig(InitFixtures.OldConfig);
        ScriptedPrompter prompter = init.Prompter(false);

        int exit = await init.InvokeAsync(init.Seams(prompter));

        Assert.Equal((CliExitCodes.Ok, 1), (exit, prompter.Asked.Count));
        Assert.Equal(old, File.ReadAllBytes(init.Paths.ConfigPath()));
    }

    [Fact]
    public async Task ShouldReplaceConfig_WhenOldFileIsInvalid() {
        init.WriteOldConfig("version: [\n");

        int exit = await init.InvokeAsync(init.Seams(init.Prompter([true, .. init.WebsiteFlow]), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Equal([InitFixtures.Role], init.Saved.Targets.Subfields);
    }

    [Fact]
    public async Task ShouldWriteNothing_BeforeLastStep() {
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        ScriptedPrompter prompter = init.Prompter(init.SmtpFlow(server));
        int asks = 0;
        prompter.OnAsk = () => {
            asks++;
            Assert.False(File.Exists(init.Paths.ConfigPath()));
            Assert.False(File.Exists(init.Paths.SecretsPath()));
            Assert.Empty(init.Store.Values);
        };

        int exit = await init.InvokeAsync(init.Seams(prompter, InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Ok, init.SmtpFlow(server).Length), (exit, asks));
    }

    [Fact]
    public async Task ShouldWriteSecretsBeforeConfig() {
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        List<bool> configAtWrite = [];
        init.Store.AfterWrite = () => configAtWrite.Add(File.Exists(init.Paths.ConfigPath()));

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.SmtpFlow(server)), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Equal([false, false], configAtWrite);
        Assert.True(File.Exists(init.Paths.ConfigPath()));
    }

    [Fact]
    public async Task ShouldNotWriteConfig_WhenSecretWriteFails() {
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        int writes = 0;
        init.Store.AfterWrite = () => {
            if (++writes == 2) {
                throw new SecretStoreUnavailableException(Credentials.SmtpPasswordName, FakeSecretStore.FailureReason);
            }
        };

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.SmtpFlow(server)), InitHarness.Listing()));

        Assert.Equal(CliExitCodes.Failure, exit);
        Assert.False(File.Exists(init.Paths.ConfigPath()));
        Assert.EndsWith(
            InitHarness.Lines(english.InitSecretStoreFailed, english.InitSecretsKept), init.Output, StringComparison.Ordinal);
        Assert.Equal("init.failed secret_store_failed", CommandFixtures.LoggedFailure(init.Paths));
    }

    [Fact]
    public async Task ShouldPrintSaveFailure_WhenConfigDirIsNotWritable() {
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        init.Store.AfterWrite = () =>
            File.SetUnixFileMode(init.Paths.ConfigDir(), UnixFileMode.UserRead | UnixFileMode.UserExecute);
        int exit;
        try {
            exit = await init.InvokeAsync(init.Seams(init.Prompter(init.SmtpFlow(server)), InitHarness.Listing()));
        } finally {
            File.SetUnixFileMode(init.Paths.ConfigDir(), FileModes.OwnerOnlyDir);
        }

        Assert.Equal(CliExitCodes.Failure, exit);
        Assert.EndsWith(
            InitHarness.Lines(english.InitSaveFailed(init.Paths.ConfigPath()), english.InitSecretsKept),
            init.Output,
            StringComparison.Ordinal);
        Assert.Equal("init.failed save_failed", CommandFixtures.LoggedFailure(init.Paths));
    }

    [Theory]
    [InlineData(Locales.English, false, false, "Cannot read {0}. Fix or remove the file, then run jobranger init again.")]
    [InlineData(Locales.Norwegian, false, false, "Kan ikke lese {0}. Rett eller fjern filen, og kjør jobranger init på nytt.")]
    [InlineData(Locales.English, true, false, "Cannot read {0}. Fix or remove the file, then run jobranger init again.")]
    [InlineData(Locales.English, false, true, "Cannot read {0}. Fix or remove the file, then run jobranger init again.")]
    public async Task ShouldStopBeforeSecretPrompt_WhenSecretsFileIsUnreadable(
        string language, bool hasStore, bool isLink, string line) {
        init.SetLanguage(language);
        Directory.CreateDirectory(init.Paths.ConfigDir());
        WriteUnreadableSecrets(isLink);
        ScriptedPrompter prompter = init.Prompter(InitFixtures.Anthropic);

        int exit = await init.InvokeAsync(InitFixtures.Seams(prompter, hasStore ? init.Store : null));

        Assert.Equal((CliExitCodes.Failure, Located(line, init.Paths.SecretsPath())), (exit, init.LastLine));
        Assert.Single(prompter.Asked);
    }

    [Fact]
    public async Task ShouldSkipSecretsFile_WhenNoSecretIsNeeded() {
        Directory.CreateDirectory(init.Paths.ConfigDir());
        File.WriteAllText(init.Paths.SecretsPath(), corruptSecrets);

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.OllamaFlow), InitHarness.OllamaListing()));

        Assert.Equal(CliExitCodes.Ok, exit);
    }

    [Theory]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserExecute)]
    [InlineData(UnixFileMode.UserWrite)]
    public async Task ShouldStopBeforeFirstPrompt_WhenConfigDirIsLocked(UnixFileMode mode) {
        string configDir = init.Paths.ConfigDir();
        Directory.CreateDirectory(configDir);
        ScriptedPrompter prompter = init.Prompter();

        int exit = await InvokeWithModeAsync(configDir, mode, prompter);

        Assert.Equal(
            (CliExitCodes.Failure, InitHarness.Lines(english.InitSaveFailed(configDir)), 0),
            (exit, init.Output, prompter.Asked.Count));
    }

    [Fact]
    public async Task ShouldNameParent_WhenMissingConfigDirCannotBeCreated() {
        string parent = init.TempFile("parent");
        string configDir = Path.Combine(parent, "jobranger");
        Directory.CreateDirectory(parent);
        init.Environment[Locales.ConfigDirVariable] = configDir;
        ScriptedPrompter prompter = init.Prompter();

        int exit = await InvokeWithModeAsync(parent, UnixFileMode.UserRead | UnixFileMode.UserExecute, prompter);

        Assert.Equal(
            (CliExitCodes.Failure, InitHarness.Lines(english.InitSaveFailed(parent)), 0),
            (exit, init.Output, prompter.Asked.Count));
    }

    [Fact]
    public async Task ShouldAskStartOverFirst_WhenConfigDirIsLocked() {
        byte[] old = init.WriteOldConfig(InitFixtures.OldConfig);
        ScriptedPrompter prompter = init.Prompter(false);

        int exit = await InvokeWithModeAsync(
            init.Paths.ConfigDir(), UnixFileMode.UserRead | UnixFileMode.UserExecute, prompter);

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Equal(old, File.ReadAllBytes(init.Paths.ConfigPath()));
    }

    [Fact]
    public async Task ShouldLeaveConfigDirMissing_WhenCancelledAtFirstPrompt() {
        ScriptedPrompter prompter = init.Prompter(ScriptedPrompter.Cancel);

        int exit = await init.InvokeAsync(init.Seams(prompter));

        Assert.Equal(RunCommandFixtures.Interrupted, exit);
        Assert.False(Directory.Exists(init.Paths.ConfigDir()));
    }

    [Theory]
    [InlineData(Locales.English, "✕ {0} is a file. jobranger needs a folder at that path. Move the file, then run jobranger init again.")]
    [InlineData(Locales.Norwegian, "✕ {0} er en fil. jobranger trenger en mappe på den stien. Flytt filen, og kjør jobranger init på nytt.")]
    public async Task ShouldNameFile_WhenFileIsAtConfigDir(string language, string line) {
        init.SetLanguage(language);
        string configDir = init.TempFile("taken");
        File.WriteAllText(configDir, string.Empty);
        init.Environment[Locales.ConfigDirVariable] = configDir;
        ScriptedPrompter prompter = init.Prompter();

        int exit = await init.InvokeAsync(init.Seams(prompter));

        Assert.Equal(
            (CliExitCodes.Failure, InitHarness.Lines(Located(line, configDir)), 0),
            (exit, init.Output, prompter.Asked.Count));
    }

    [Fact]
    public async Task ShouldNameFile_WhenFileIsAboveConfigDir() {
        init.Environment[Locales.ConfigDirVariable] = Path.Combine(init.Profile, "config");
        ScriptedPrompter prompter = init.Prompter();

        int exit = await init.InvokeAsync(init.Seams(prompter));

        Assert.Equal((CliExitCodes.Failure, 0), (exit, prompter.Asked.Count));
        Assert.StartsWith($"✕ {init.Profile} is a file.", init.LastLine, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Locales.English, "Another jobranger process is using {0}. Try again when it ends.")]
    [InlineData(Locales.Norwegian, "En annen jobranger-prosess bruker {0}. Prøv igjen når den er ferdig.")]
    public async Task ShouldSayFileIsBusy_WhenSecretsLockIsHeld(string language, string line) {
        init.SetLanguage(language);
        init.Store.AfterWrite = () => throw new TimeoutException("lock still held");

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(init.WebsiteFlow), InitHarness.Listing()));

        Assert.Equal((CliExitCodes.Failure, Located(line, init.Paths.SecretsPath())), (exit, init.LastLine));
        Assert.Equal("init.failed save_failed", CommandFixtures.LoggedFailure(init.Paths));
    }

    [Fact]
    public async Task ShouldOpenStoreOnce_WhenTwoSecretsAreAsked() {
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        ProbeStore probe = new(init.Store);
        int opens = 0;
        bool? canPrompt = null;
        InitSeams seams = init.Seams(init.Prompter(init.SmtpFlow(server)), InitHarness.Listing()) with {
            OpenSecrets = (_, prompts, _) => {
                opens++;
                canPrompt = prompts;
                return Task.FromResult<ISecretStore?>(probe);
            },
        };

        int exit = await init.InvokeAsync(seams);

        Assert.Equal((CliExitCodes.Ok, 1, 1, true), (exit, opens, probe.Reads, canPrompt));
    }

    [Fact]
    public async Task ShouldFinishSave_WhenCancelledDuringSave() {
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        init.Store.AfterWrite = init.Cancel.Cancel;
        ProbeStore checking = new(init.Store);
        ScriptedPrompter prompter = init.Prompter(init.SmtpFlow(server));

        int exit = await init.InvokeAsync(InitFixtures.Seams(prompter, checking, [InitHarness.Listing()]));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.True(File.Exists(init.Paths.ConfigPath()));
        Assert.Equal(ConfigFixtures.Secret, init.Store.Values[Credentials.SmtpPasswordName]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(18)]
    public async Task ShouldSaveNothing_WhenCancelled(int position) {
        byte[] old = init.WriteOldConfig(InitFixtures.OldConfig);
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        object[] answers = [true, .. init.SmtpFlow(server)];
        answers[position] = ScriptedPrompter.Cancel;

        int exit = await init.InvokeAsync(init.Seams(init.Prompter(answers), InitHarness.Listing()));

        Assert.Equal((RunCommandFixtures.Interrupted, english.InitCancelled), (exit, init.LastLine));
        Assert.Equal(old, File.ReadAllBytes(init.Paths.ConfigPath()));
        Assert.Empty(init.Store.Values);
        Assert.False(File.Exists(init.Paths.SecretsPath()));
    }

    [Fact]
    public async Task ShouldNotOpenStore_WhenNoSecretIsNeeded() {
        int opens = 0;
        InitSeams seams = init.Seams(init.Prompter(init.OllamaFlow), InitHarness.OllamaListing()) with {
            OpenSecrets = (_, _, _) => {
                opens++;
                return Task.FromResult<ISecretStore?>(new FakeSecretStore { IsBroken = true });
            },
        };

        int exit = await init.InvokeAsync(seams);

        Assert.Equal((CliExitCodes.Ok, 0), (exit, opens));
        Assert.DoesNotContain("Secrets stored", init.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Locales.English, "✕ The secret store did not answer. Unlock it, or keep secrets in a file with: export JOBRANGER_SECRET_STORE=file")]
    [InlineData(Locales.Norwegian, "✕ Hemmelighetslageret svarte ikke. Lås det opp, eller ha hemmeligheter i en fil med: export JOBRANGER_SECRET_STORE=file")]
    public async Task ShouldPointAtFileStore_WhenStoreFailsEarly(string language, string line) {
        init.SetLanguage(language);
        ScriptedPrompter prompter = init.Prompter(InitFixtures.Anthropic);

        int exit = await init.InvokeAsync(InitFixtures.Seams(prompter, new FakeSecretStore { IsBroken = true }));

        Assert.Equal((CliExitCodes.Failure, line), (exit, init.LastLine));
        Assert.False(File.Exists(init.Paths.ConfigPath()));
    }

    [Fact]
    public async Task ShouldNumberHeaders_FromStepCount() {
        StubStep step = new(ValidConfig);
        SetupContext context = init.Context(init.Prompter());
        SetupEntry setup = new(context, [step, step], new SetupSecrets(context));

        await setup.RunInitAsync(TestContext.Current.CancellationToken);

        Assert.Contains(english.InitStepHeader(1, 2, StubStep.Name), init.Output, StringComparison.Ordinal);
        Assert.Contains(english.InitStepHeader(2, 2, StubStep.Name), init.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldSaveNothing_WhenFinalStateIsInvalid() {
        StubStep step = new(
            config => ValidConfig(config) with { Schedule = new ScheduleConfig { Times = ["25:00"] } }, ConfigFixtures.Secret);
        SetupContext context = init.Context(init.Prompter());
        SetupEntry setup = new(context, [step], new SetupSecrets(context));

        await Assert.ThrowsAsync<ConfigInvalidException>(() => setup.RunInitAsync(TestContext.Current.CancellationToken));

        Assert.False(File.Exists(init.Paths.ConfigPath()));
        Assert.Empty(init.Store.Values);
    }

    [Theory]
    [InlineData(Locales.English, "transcript.en.txt")]
    [InlineData(Locales.Norwegian, "transcript.nb.txt")]
    public async Task ShouldMatchTranscript(string language, string golden) {
        init.SetLanguage(language);
        await using FakeSmtpServer server = InitHarness.AcceptingServer();
        ScriptedPrompter prompter = init.Prompter(init.SmtpFlow(server));

        int exit = await init.InvokeAsync(InitFixtures.Seams(prompter, null, [InitHarness.Listing()]));

        Assert.Equal(CliExitCodes.Ok, exit);
        Assert.Equal(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "Init", golden)),
            init.Output.Replace(init.TempPath, homeMark, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldContractProfilePath_WhenConfigIsSaved() {
        SetupEntry setup = SetupEntry.Init(init.Context(init.Prompter(init.WebsiteFlow), InitHarness.Listing()));

        await setup.RunInitAsync(TestContext.Current.CancellationToken);

        Assert.Contains("path: ~/profile.yaml", File.ReadAllText(init.Paths.ConfigPath()), StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldHideAnswers_WhenStateIsPrinted() {
        NotifyConfig notify = new() { Email = DeliveryFixtures.ResendEmail };
        InitState state = InitState.Empty with { Config = InitState.Empty.Config with { Notify = notify } };

        string printed = state.ToString();

        Assert.DoesNotContain(DeliveryFixtures.Address, printed, StringComparison.Ordinal);
    }

    private static string Located(string line, string path) => line.Replace("{0}", path, StringComparison.Ordinal);

    private void WriteUnreadableSecrets(bool isLink) {
        if (!isLink) {
            File.WriteAllText(init.Paths.SecretsPath(), corruptSecrets);
            return;
        }
        string target = init.TempFile("target.json");
        File.WriteAllText(target, validSecrets);
        File.CreateSymbolicLink(init.Paths.SecretsPath(), target);
    }

    private async Task<int> InvokeWithModeAsync(string dir, UnixFileMode mode, ScriptedPrompter prompter) {
        File.SetUnixFileMode(dir, mode);
        try {
            return await init.InvokeAsync(init.Seams(prompter));
        } finally {
            File.SetUnixFileMode(dir, FileModes.OwnerOnlyDir);
        }
    }

    private AppConfig ValidConfig(AppConfig config) => config with { Profile = new ProfileRef(init.Profile) };

    private sealed class StubStep(Func<AppConfig, AppConfig> change, string? secret = null) : ISetupStep {
        public const string Name = "t";

        public string Title => Name;

        public Task<InitState> RunAsync(InitState state, CancellationToken cancellationToken) {
            InitState changed = state with { Config = change(state.Config) };
            return Task.FromResult(secret is null ? changed : changed.WithSecret(Credentials.SmtpPasswordName, secret));
        }
    }

    private sealed class ProbeStore(FakeSecretStore inner) : ISecretStore {
        public int Reads { get; private set; }

        public Task<string?> ReadAsync(string name, CancellationToken cancellationToken) {
            Reads++;
            return inner.ReadAsync(name, cancellationToken);
        }

        public Task WriteAsync(string name, string value, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            return inner.WriteAsync(name, value, cancellationToken);
        }

        public Task DeleteAsync(string name, CancellationToken cancellationToken) =>
            inner.DeleteAsync(name, cancellationToken);
    }
}
