using System.Collections.Concurrent;
using System.Text.Json;
using HerdrShell.Core.Client;
using HerdrShell.Core.Protocol;
using HerdrShell.Core.Transport;

namespace HerdrShell.Core.Tests.Fakes;

public sealed record ReceivedRequest(string Id, string Method, JsonElement Params);

/// <summary>
/// Scriptable fake mirroring the verified herdr 0.8.2 connection model:
/// one request per connection — the server responds and closes — except
/// events.subscribe, which turns the connection into a long-lived event
/// stream. Handlers return the result payload; FakeHerdrError produces an
/// error response; null withholds the response and closes (drop test).
/// </summary>
public sealed class FakeHerdrEndpoint : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Func<JsonElement, object?>> _handlers = new();
    private readonly ConcurrentBag<Connection> _connections = [];
    private readonly ConcurrentBag<Connection> _streams = [];
    private int _connectionCount;

    public ConcurrentQueue<ReceivedRequest> Requests { get; } = new();

    public int ConnectionCount => _connectionCount;

    public HerdrTransportFactory Factory => Connect;

    private Task<IHerdrTransport> Connect(CancellationToken ct)
    {
        Interlocked.Increment(ref _connectionCount);
        var (client, server) = InMemoryHerdrTransport.CreatePair();
        _connections.Add(new Connection(server, this));
        return Task.FromResult<IHerdrTransport>(client);
    }

    public void Handle(string method, Func<JsonElement, object?> handler) =>
        _handlers[method] = handler;

    public void HandleOk(string method) => Handle(method, _ => new { type = "ok" });

    /// <summary>Pushes an event envelope into every active stream connection.</summary>
    public async Task SendEventAsync(string eventName, object data)
    {
        foreach (var stream in _streams)
            await stream.SendEventAsync(eventName, data);
    }

    /// <summary>Closes every active stream connection (server restart simulation).</summary>
    public async Task CloseStreamsAsync()
    {
        foreach (var stream in _streams)
            await stream.CloseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
            await connection.CloseAsync();
    }

    private sealed class Connection
    {
        private readonly IHerdrTransport _transport;
        private readonly FakeHerdrEndpoint _endpoint;
        private readonly Task _loop;

        public Connection(IHerdrTransport transport, FakeHerdrEndpoint endpoint)
        {
            _transport = transport;
            _endpoint = endpoint;
            _loop = Task.Run(RunAsync);
        }

        public Task SendEventAsync(string eventName, object data) =>
            _transport.WriteLineAsync(
                JsonSerializer.Serialize(new { @event = eventName, data }, HerdrJson.Options));

        public async Task CloseAsync()
        {
            await _transport.DisposeAsync();
            await _loop;
        }

        private async Task RunAsync()
        {
            while (await _transport.ReadLineAsync() is { } line)
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var id = root.GetProperty("id").GetString()!;
                var method = root.GetProperty("method").GetString()!;
                var parameters = root.GetProperty("params").Clone();
                _endpoint.Requests.Enqueue(new ReceivedRequest(id, method, parameters));

                if (method == HerdrMethods.EventsSubscribe)
                {
                    await _transport.WriteLineAsync(JsonSerializer.Serialize(
                        new { id, result = new { type = "subscription_started" } },
                        HerdrJson.Options));
                    _endpoint._streams.Add(this);
                    continue; // stream mode: connection stays open for events
                }

                if (_endpoint._handlers.TryGetValue(method, out var handler) &&
                    handler(parameters) is { } outcome)
                {
                    var payload = outcome is FakeHerdrError error
                        ? JsonSerializer.Serialize(
                            new { id, error = new { error.Code, error.Message } },
                            HerdrJson.Options)
                        : JsonSerializer.Serialize(new { id, result = outcome }, HerdrJson.Options);
                    await _transport.WriteLineAsync(payload);
                }
                // One request per connection: close whether or not we responded.
                await _transport.DisposeAsync();
                return;
            }
        }
    }
}

public sealed record FakeHerdrError(string Code, string Message);
