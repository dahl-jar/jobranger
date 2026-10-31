namespace Jobranger.Messaging.Payloads;

internal readonly record struct SchemaFailure(string Path, string Keyword);
