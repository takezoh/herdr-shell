using HerdrShell.Core.Protocol;

namespace HerdrShell.Core.Supervision;

public enum SessionPhase
{
    Running,
    Waiting,
    Done,
}

public sealed record AgentSessionSummary(
    string PaneId,
    string WorkspaceId,
    string TabId,
    SessionPhase Phase,
    bool HumanInput,
    string? Title = null,
    string? Agent = null);

public sealed record HerdrSupervisionSnapshot(
    IReadOnlyList<AgentSessionSummary> Sessions,
    bool ConnectionFailed,
    string? ConnectionFailureReason)
{
    public static HerdrSupervisionSnapshot Empty { get; } = new(
        Array.Empty<AgentSessionSummary>(),
        ConnectionFailed: false,
        ConnectionFailureReason: null);

    public int AttentionCount => Sessions.Count(session => session.HumanInput);
}

/// <summary>Inbound events reduced by SupervisionReducer.</summary>
public abstract record SupervisionEvent;

/// <summary>Full resync from agent.list; replaces all tracked sessions.</summary>
public sealed record EvtAgentsListed(IReadOnlyList<HerdrPaneAgent> Agents) : SupervisionEvent;

/// <summary>pane_created / pane_updated carrying the full pane projection.</summary>
public sealed record EvtPaneUpserted(HerdrPaneAgent Pane) : SupervisionEvent;

/// <summary>pane_agent_status_changed — partial update, title may be null.</summary>
public sealed record EvtAgentStatusChanged(
    string PaneId,
    string WorkspaceId,
    HerdrAgentStatus Status,
    string? Title,
    string? Agent) : SupervisionEvent;

/// <summary>pane_closed / pane_exited.</summary>
public sealed record EvtPaneRemoved(string PaneId) : SupervisionEvent;

public sealed record EvtConnectionFailed(string Reason) : SupervisionEvent;

public sealed record EvtConnectionRestored : SupervisionEvent;
