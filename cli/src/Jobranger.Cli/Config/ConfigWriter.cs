using System.Text.Json.Nodes;
using Jobranger.Cli.Catalogue;

namespace Jobranger.Cli.Config;

internal static class ConfigWriter {
    public static JsonObject ToTree(AppConfig config, string home) {
        JsonObject tree = new() {
            [ConfigKeys.Version] = ConfigReader.Version,
            [ConfigKeys.Profile] = new JsonObject {
                [ConfigKeys.Path] = HomePath.Contract(config.Profile.Path, home),
                [ConfigKeys.CvName] = config.Profile.CvName,
                [ConfigKeys.CvReadAt] = config.Profile.CvReadAt,
            },
            [ConfigKeys.Targets] = new JsonObject {
                [ConfigKeys.Subfields] = Texts(config.Targets.Subfields),
                [ConfigKeys.Limits] = new JsonObject {
                    [ConfigKeys.Levels] = Texts(config.Targets.Limits.Levels),
                    [ConfigKeys.Years] = Texts(config.Targets.Limits.Years),
                    [ConfigKeys.HideMasters] = config.Targets.Limits.HideMasters,
                },
                [ConfigKeys.Places] = Texts(config.Targets.Places),
                [ConfigKeys.Relocate] = config.Targets.Relocate,
            },
            [ConfigKeys.Sources] = new JsonObject {
                [ConfigKeys.Nav] = config.Sources.Nav,
                [ConfigKeys.Employers] = config.Sources.Employers,
                [ConfigKeys.Finn] = config.Sources.Finn,
                [ConfigKeys.LinkedIn] = config.Sources.LinkedIn,
                [ConfigKeys.EmployerFeeds] = new JsonArray([.. config.Sources.EmployerFeeds.Select(Feed)]),
            },
            [ConfigKeys.Schedule] = new JsonObject { [ConfigKeys.Times] = Texts(config.Schedule.Times) },
            [ConfigKeys.Model] = new JsonObject {
                [ConfigKeys.Provider] = config.Model.Provider is ProviderName provider ? WireNames.Of(provider) : null,
                [ConfigKeys.Model] = config.Model.Model,
                [ConfigKeys.OllamaUrl] = config.Model.OllamaUrl,
                [ConfigKeys.MonthlyLimitUsd] = config.Model.MonthlyLimitUsd,
                [ConfigKeys.Price] = config.Model.Price is PriceOverride price
                    ? new JsonObject {
                        [ConfigKeys.InputPerMtok] = price.InputPerMtok,
                        [ConfigKeys.OutputPerMtok] = price.OutputPerMtok,
                    }
                    : null,
            },
            [ConfigKeys.Notify] = new JsonObject {
                [ConfigKeys.Website] = config.Notify.Website,
                [ConfigKeys.Email] = new JsonObject {
                    [ConfigKeys.On] = config.Notify.Email.On,
                    [ConfigKeys.Address] = config.Notify.Email.Address,
                    [ConfigKeys.Via] = WireNames.Of(config.Notify.Email.Via),
                    [ConfigKeys.SmtpUrl] = config.Notify.Email.SmtpUrl,
                    [ConfigKeys.SmtpUser] = config.Notify.Email.SmtpUser,
                    [ConfigKeys.Sender] = config.Notify.Email.Sender,
                },
            },
        };
        if (config.Language is Text.Language language) {
            tree[ConfigKeys.Language] = WireNames.Of(language);
        }
        return tree;
    }

    private static JsonObject Feed(EmployerFeed feed) => new() {
        [ConfigKeys.Kind] = feed.Kind,
        [ConfigKeys.Key] = feed.Key,
        [ConfigKeys.Company] = feed.Company,
        [ConfigKeys.Board] = feed.Board,
    };

    private static JsonArray Texts(IEnumerable<string> values) =>
        new([.. values.Select(value => JsonValue.Create(value))]);
}
