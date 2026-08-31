using HerdrShell.Core.Protocol;

namespace HerdrShell.Core.Client;

/// <summary>
/// Typed request surface over the herdr socket API used by supervision and
/// jump-back. Each call is one connection (the server closes after
/// responding); event subscription lives on HerdrEventStream instead.
/// Faked in tests; HerdrRequestClient is the wire implementation.
/// </summary>
public interface IHerdrControl
{
    Task<HerdrPong> PingAsync(CancellationToken ct = default);

    Task<IReadOnlyList<HerdrPaneAgent>> ListAgentsAsync(CancellationToken ct = default);

    /// <summary>agent.focus — activates workspace, tab and pane of the target agent.</summary>
    Task FocusAgentAsync(string target, CancellationToken ct = default);

    /// <summary>
    /// client.window_title.set — the foreground attached client stamps the
    /// hosting terminal's window/tab title via OSC.
    /// </summary>
    Task<ClientWindowTitleResult> SetClientWindowTitleAsync(string title, CancellationToken ct = default);

    Task<ClientWindowTitleResult> ClearClientWindowTitleAsync(CancellationToken ct = default);
}
