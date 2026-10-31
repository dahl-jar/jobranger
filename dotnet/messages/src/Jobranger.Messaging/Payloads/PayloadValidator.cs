using System.Text.Json;
using Json.Schema;

namespace Jobranger.Messaging.Payloads;

internal static class PayloadValidator {
    private static readonly HashSet<string> descents = new(StringComparer.Ordinal) { PayloadSchema.Properties, PayloadSchema.Items, PayloadSchema.Reference };

    private static readonly EvaluationOptions options = new() {
        OutputFormat = OutputFormat.Hierarchical,
        RequireFormatValidation = true,
    };

    public static void Validate(JsonElement instance, string definition, string? messageType) {
        SchemaFailure? failure = Find(instance, definition);
        if (failure is { } found) {
            throw new ContractException(messageType, found.Path, found.Keyword);
        }
    }

    public static bool IsValid(string definition, string text) =>
        Find(JsonSerializer.SerializeToElement(text, MessagesJsonContext.Default.String), definition) is null;

    private static SchemaFailure? Find(JsonElement instance, string definition) {
        SchemaFailure? oversize = LengthLimits.Find(instance, PayloadSchema.DefinitionNodes[definition]!.AsObject(), ContractException.RootPath);
        if (oversize is not null) {
            return oversize;
        }
        EvaluationResults results = PayloadSchema.For(definition).Evaluate(instance, options);
        if (!results.IsValid) {
            return FirstError(results, instance);
        }
        return definition == HiddenCountRules.Definition ? HiddenCountRules.Find(instance) : null;
    }

    private static SchemaFailure FirstError(EvaluationResults results, JsonElement instance) {
        (EvaluationResults node, string keyword) = Leaf(results)!.Value;
        return new SchemaFailure(InstancePaths.FromPointer(node.InstanceLocation.ToString(), instance), keyword);
    }

    private static (EvaluationResults Node, string Keyword)? Leaf(EvaluationResults node) {
        string? keyword = node.Errors?.Keys.FirstOrDefault(name => !descents.Contains(name));
        if (keyword is not null) {
            return (node, keyword);
        }
        return (node.Details ?? [])
            .Where(child => !child.IsValid)
            .Select(Leaf)
            .FirstOrDefault(found => found is not null);
    }
}
