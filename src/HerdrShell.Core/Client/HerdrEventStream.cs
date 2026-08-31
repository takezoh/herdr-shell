using System.Text.Json;
using HerdrShell.Core.Protocol;
using HerdrShell.Core.Transport;

namespace HerdrShell.Core.Client;

/// <summary>
/// The one connection shape the server keeps open: events.subscribe turns the
/// connection into an event stream (no further requests are accepted on it).
/// </summary>
public sealed class HerdrEventStream : IAsyncDisposable
{
    private readonly IHerdrTransport _transport;

    private HerdrEventStream(IHerdrTransport transport) => _transport = transport;

    public static async Task<HerdrEventStream> OpenAsync(
        HerdrTransportFactory connect,
        IReadOnlyList<string> subscriptionTypes,
        CancellationToken ct = default)
    {
        var transport = await connect(ct).ConfigureAwait(false);
        try
        {
            var line = JsonSerializer.Serialize(
                new HerdrRequestClient.RequestEnvelope(
                    "1",
                    HerdrMethods.EventsSubscribe,
                    new { subscriptions = subscriptionTypes.Select(type => new { type }).ToList() }),
                HerdrJson.Options);
            await transport.WriteLineAsync(line, ct).ConfigureAwait(false);

            while (await transport.ReadLineAsync(ct).ConfigureAwait(false) is { } received)
            {
                if (string.IsNullOrWhiteSpace(received))
                    continue;
                using var doc = JsonDocument.Parse(received);
                var root = doc.RootElement;
                if (!root.TryGetProperty("id", out _))
                    continue;
                if (root.TryGetProperty("error", out var error))
                    throw HerdrRequestClient.ToException(error);
                return new HerdrEventStream(transport);
            }
            throw new IOException("herdr closed the connection before confirming the subscription");
        }
        catch
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Pumps event envelopes until end-of-stream (server gone) or
    /// cancellation. Returning normally means the connection closed and the
    /// caller should reconnect.
    /// </summary>
    public async Task ReadAllAsync(
        Action<HerdrEventEnvelope> onEvent, CancellationToken ct = default)
    {
        while (await _transport.ReadLineAsync(ct).ConfigureAwait(false) is { } received)
        {
            if (string.IsNullOrWhiteSpace(received))
                continue;
            using var doc = JsonDocument.Parse(received);
            var root = doc.RootElement;
            if (!root.TryGetProperty("event", out var eventProp) ||
                !root.TryGetProperty("data", out var data))
                continue;
            var name = eventProp.GetString();
            if (name is not null)
                onEvent(new HerdrEventEnvelope(name, data.Clone()));
        }
    }

    public ValueTask DisposeAsync() => _transport.DisposeAsync();
}
