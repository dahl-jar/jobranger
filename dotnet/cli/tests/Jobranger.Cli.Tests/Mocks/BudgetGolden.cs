using System.Text.Json;
using Jobranger.Cli.Features.Budget;

namespace Jobranger.Cli.Tests.Mocks;

internal static class BudgetGolden {
    private const string goldenFolder = "Golden";
    private const string budgetFolder = "Budget";
    private const string fileName = "estimates.json";

    private static readonly JsonElement root = Load();

    public static ModelPrices Prices { get; } = ReadPrices(root.GetProperty("prices"));

    public static decimal CacheWriteUsd { get; } = root.GetProperty("cache_write_usd").GetDecimal();

    public static decimal AdUsd { get; } = root.GetProperty("ad_cost_usd").GetProperty("single").GetDecimal();

    public static decimal BatchAdUsd { get; } = root.GetProperty("ad_cost_usd").GetProperty("batch").GetDecimal();

    public static ModelPrices RecheckPrices { get; } = ReadPrices(root.GetProperty("recheck").GetProperty("prices"));

    public static int RecheckAds { get; } = root.GetProperty("recheck").GetProperty("ads").GetInt32();

    public static int RecheckSystemChars { get; } = root.GetProperty("recheck").GetProperty("system_chars").GetInt32();

    public static decimal RecheckUsd { get; } = root.GetProperty("recheck").GetProperty("cost_usd").GetDecimal();

    public static TheoryData<decimal, decimal, decimal, int> Affordable() {
        TheoryData<decimal, decimal, decimal, int> rows = [];
        foreach (JsonElement row in root.GetProperty("affordable").EnumerateArray()) {
            rows.Add(
                row.GetProperty("spent_usd").GetDecimal(),
                row.GetProperty("limit_usd").GetDecimal(),
                row.GetProperty("per_ad_usd").GetDecimal(),
                row.GetProperty("count").GetInt32());
        }
        return rows;
    }

    private static ModelPrices ReadPrices(JsonElement prices) => new(
        prices.GetProperty("input").GetDecimal(),
        prices.GetProperty("output").GetDecimal(),
        prices.GetProperty("cache_read").GetDecimal(),
        prices.GetProperty("cache_write_5m").GetDecimal(),
        prices.GetProperty("cache_write_1h").GetDecimal());

    private static JsonElement Load() {
        string path = Path.Combine(AppContext.BaseDirectory, goldenFolder, budgetFolder, fileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }
}
