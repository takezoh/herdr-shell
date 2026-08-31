using System.Text.Json;
using HerdrShell.Core.Protocol;

namespace HerdrShell.Core.Client;

/// <summary>
/// Request executor for the herdr socket API. The server serves exactly one
/// request per connection (verified against herdr 0.8.2: the connection is
/// closed right after the response, for every method except events.subscribe),
/// so each call connects, sends, reads its response, and disposes.
/// </summary>
public sealed class HerdrRequestClient : IHerdrControl
{
    private readonly HerdrTransportFactory _connect;

    public HerdrRequestClient(HerdrTransportFactory connect) => _connect = connect;

    public async Task<JsonElement> SendAsync(
        string method, object? parameters, CancellationToken ct = default)
    {
        await using var transport = await _connect(ct).ConfigureAwait(false);
        var line = JsonSerializer.Serialize(
            new RequestEnvelope("1", method, parameters ?? EmptyParams.Instance),
            HerdrJson.Options);
        await transport.WriteLineAsync(line, ct).ConfigureAwait(false);

        while (await transport.ReadLineAsync(ct).ConfigureAwait(false) is { } received)
        {
            if (string.IsNullOrWhiteSpace(received))
                continue;
            using var doc = JsonDocument.Parse(received);
            var root = doc.RootElement;
            if (!root.TryGetProperty("id", out _))
                continue; // stray event envelope; not ours
            if (root.TryGetProperty("error", out var error))
                throw ToException(error);
            if (root.TryGetProperty("result", out var result))
                return result.Clone();
            throw new InvalidOperationException("response without result or error");
        }
        throw new IOException($"herdr closed the connection before responding to {method}");
    }

    public async Task<HerdrPong> PingAsync(CancellationToken ct = default)
    {
        var result = await SendAsync(HerdrMethods.Ping, null, ct).ConfigureAwait(false);
        return result.Deserialize<HerdrPong>(HerdrJson.Options)
            ?? throw new InvalidOperationException("empty pong");
    }

    public async Task<IReadOnlyList<HerdrPaneAgent>> ListAgentsAsync(CancellationToken ct = default)
    {
        var result = await SendAsync(HerdrMethods.AgentList, null, ct).ConfigureAwait(false);
        if (!result.TryGetProperty("agents", out var agents))
            throw new InvalidOperationException("agent_list result missing 'agents'");
        return agents.Deserialize<List<HerdrPaneAgent>>(HerdrJson.Options)
            ?? throw new InvalidOperationException("agent_list agents not an array");
    }

    public Task FocusAgentAsync(string target, CancellationToken ct = default) =>
        SendAsync(HerdrMethods.AgentFocus, new { target }, ct);

    public async Task<ClientWindowTitleResult> SetClientWindowTitleAsync(
        string title, CancellationToken ct = default)
    {
        var result = await SendAsync(HerdrMethods.ClientWindowTitleSet, new { title }, ct)
            .ConfigureAwait(false);
        return ParseWindowTitleResult(result);
    }

    public async Task<ClientWindowTitleResult> ClearClientWindowTitleAsync(
        CancellationToken ct = default)
    {
        var result = await SendAsync(HerdrMethods.ClientWindowTitleClear, null, ct)
            .ConfigureAwait(false);
        return ParseWindowTitleResult(result);
    }

    internal static HerdrApiException ToException(JsonElement error)
    {
        var code = error.TryGetProperty("code", out var c)
            ? c.GetString() ?? "unknown"
            : "unknown";
        var message = error.TryGetProperty("message", out var m)
            ? m.GetString() ?? "unknown herdr error"
            : "unknown herdr error";
        return new HerdrApiException(code, message);
    }

    private static ClientWindowTitleResult ParseWindowTitleResult(JsonElement result) =>
        result.Deserialize<ClientWindowTitleResult>(HerdrJson.Options)
            ?? throw new InvalidOperationException("empty client_window_title result");

    internal sealed record RequestEnvelope(string Id, string Method, object Params);

    internal sealed class EmptyParams
    {
        public static readonly EmptyParams Instance = new();
    }
}
