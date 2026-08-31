using System.Text.Json.Serialization;

namespace HerdrShell.Core.Protocol;

public enum HerdrAgentStatus
{
    Idle,
    Working,
    Blocked,
    Done,
    Unknown,
}

/// <summary>
/// Pane/agent projection shared by agent.list results (AgentInfo) and
/// pane_created/pane_updated event payloads (PaneInfo) — both carry this
/// superset of fields; extras are ignored on deserialize.
/// </summary>
public sealed record HerdrPaneAgent(
    [property: JsonPropertyName("pane_id")] string PaneId,
    [property: JsonPropertyName("workspace_id")] string WorkspaceId,
    [property: JsonPropertyName("tab_id")] string TabId,
    [property: JsonPropertyName("agent_status")] HerdrAgentStatus AgentStatus,
    [property: JsonPropertyName("agent")] string? Agent = null,
    [property: JsonPropertyName("display_agent")] string? DisplayAgent = null,
    [property: JsonPropertyName("title")] string? Title = null,
    [property: JsonPropertyName("terminal_title")] string? TerminalTitle = null,
    [property: JsonPropertyName("focused")] bool Focused = false);

public enum ClientWindowTitleReason
{
    Set,
    Cleared,
    NoForegroundClient,
}

public sealed record ClientWindowTitleResult(
    [property: JsonPropertyName("changed")] bool Changed,
    [property: JsonPropertyName("reason")] ClientWindowTitleReason Reason);

public sealed record HerdrPong(
    [property: JsonPropertyName("protocol")] int Protocol,
    [property: JsonPropertyName("version")] string? Version);
