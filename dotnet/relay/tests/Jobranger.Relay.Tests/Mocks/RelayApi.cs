using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Jobranger.Relay.Tests.Mocks;

internal sealed class RelayApi(HttpClient client) : IDisposable {
    public const string ClientHeader = "CF-Connecting-IP";
    public const string JsonType = "application/json";

    private const string unknownCode = "BBBB-BBBB";

    public static RelayApi For(WebApplicationFactory<Program> factory, string clientAddress, string? peer = null) {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ClientHeader, clientAddress);
        if (peer is not null) {
            client.DefaultRequestHeaders.Add(PeerAddressFilter.PeerHeader, peer);
        }

        return new RelayApi(client);
    }

    public static async Task<JsonObject> Json(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();

    public static async Task<(HttpStatusCode Status, string? Code)> Problem(HttpResponseMessage response) {
        JsonObject body = await Json(response);
        return (response.StatusCode, (string?)body["code"]);
    }

    public static JsonObject NonceBody(string? channel) =>
        channel is null ? new() { ["v"] = 1 } : new() { ["v"] = 1, ["channel"] = channel };

    public Task<HttpResponseMessage> Post(string path, JsonObject body) => PostRaw(path, body.ToJsonString());

    public Task<HttpResponseMessage> PostRaw(string path, string body) =>
        client.PostAsync(path, new StringContent(body, Encoding.UTF8, JsonType), TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> Send(HttpRequestMessage request) =>
        client.SendAsync(request, TestContext.Current.CancellationToken);

    public async Task<string> Nonce() {
        using HttpResponseMessage response = await Post("/pair/nonce", NonceBody(null));
        JsonObject body = await Json(response);
        return (string)body["nonce"]!;
    }

    public async Task<JsonObject> StartPairing(CliKey key) {
        using HttpResponseMessage response = await Post("/pair/start", key.Proof(CliKey.PairStart, await Nonce()));
        return await Json(response);
    }

    public Task<HttpResponseMessage> Claim(string code) => Post("/pair/claim", new JsonObject { ["v"] = 1, ["code"] = code });

    public async Task Claims(int count) {
        for (int claim = 0; claim < count; claim++) {
            using HttpResponseMessage response = await Claim(unknownCode);
        }
    }

    public Task<HttpResponseMessage> WebSession(string ticket) =>
        Post("/session/web", new JsonObject { ["v"] = 1, ["ticket"] = ticket });

    public async Task<JsonObject> Paired(CliKey key) {
        JsonObject started = await StartPairing(key);
        using HttpResponseMessage claimed = await Claim((string)started["code"]!);
        return await Json(claimed);
    }

    public void Dispose() => client.Dispose();
}
