using System.Text.Json;

namespace HerdrShell.Core.Client;

public sealed record HerdrEventEnvelope(string Event, JsonElement Data)
{
    /// <summary>
    /// Broadcast events use underscores ("pane_updated") while per-pane
    /// subscription events use dots ("pane.agent_status_changed"); consumers
    /// match on the normalized underscore form.
    /// </summary>
    public string NormalizedEvent => Event.Replace('.', '_');
}

public sealed class HerdrApiException : Exception
{
    public HerdrApiException(string code, string message) : base(message) => Code = code;

    public string Code { get; }
}

/// <summary>Connects a fresh transport; the server closes it after one response.</summary>
public delegate Task<Transport.IHerdrTransport> HerdrTransportFactory(CancellationToken ct);
