using System.Text.Json.Nodes;
using Jobranger.Cli.Catalogue;

namespace Jobranger.Cli.Config;

internal static class ConfigWriter {
    public static JsonObject ToTree(AppConfig config, string home) {
        JsonObject tree = new() {
            ["version"] = ConfigReader.Version,
            ["profile"] = new JsonObject {
                ["path"] = HomePath.Contract(config.Profile.Path, home),
                ["cv_name"] = config.Profile.CvName,
                ["cv_read_at"] = config.Profile.CvReadAt,
            },
            ["targets"] = new JsonObject {
                ["subfields"] = Texts(config.Targets.Subfields),
                ["limits"] = new JsonObject {
                    ["levels"] = Texts(config.Targets.Limits.Levels),
                    ["years"] = Texts(config.Targets.Limits.Years),
                    ["hide_masters"] = config.Targets.Limits.HideMasters,
                },
                ["places"] = Texts(config.Targets.Places),
                ["relocate"] = config.Targets.Relocate,
            },
            ["sources"] = new JsonObject {
                ["nav"] = config.Sources.Nav,
                ["employers"] = config.Sources.Employers,
                ["finn"] = config.Sources.Finn,
                ["linkedin"] = config.Sources.LinkedIn,
                ["employer_feeds"] = new JsonArray([.. config.Sources.EmployerFeeds.Select(Feed)]),
            },
            ["schedule"] = new JsonObject { ["times"] = Texts(config.Schedule.Times) },
            ["model"] = new JsonObject {
                ["provider"] = config.Model.Provider is ProviderName provider ? WireNames.Of(provider) : null,
                ["model"] = config.Model.Model,
                ["ollama_url"] = config.Model.OllamaUrl,
                ["monthly_limit_usd"] = config.Model.MonthlyLimitUsd,
                ["price"] = config.Model.Price is PriceOverride price
                    ? new JsonObject {
                        ["input_per_mtok"] = price.InputPerMtok,
                        ["output_per_mtok"] = price.OutputPerMtok,
                    }
                    : null,
            },
            ["notify"] = new JsonObject {
                ["website"] = config.Notify.Website,
                ["email"] = new JsonObject {
                    ["on"] = config.Notify.Email.On,
                    ["address"] = config.Notify.Email.Address,
                    ["via"] = WireNames.Of(config.Notify.Email.Via),
                    ["smtp_url"] = config.Notify.Email.SmtpUrl,
                    ["smtp_user"] = config.Notify.Email.SmtpUser,
                    ["sender"] = config.Notify.Email.Sender,
                },
            },
        };
        if (config.Language is Text.Language language) {
            tree["language"] = WireNames.Of(language);
        }
        return tree;
    }

    private static JsonObject Feed(EmployerFeed feed) => new() {
        ["kind"] = feed.Kind,
        ["key"] = feed.Key,
        ["company"] = feed.Company,
        ["board"] = feed.Board,
    };

    private static JsonArray Texts(IEnumerable<string> values) =>
        new([.. values.Select(value => JsonValue.Create(value))]);
}
