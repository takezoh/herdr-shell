using HerdrShell.Core.Protocol;

namespace HerdrShell.Core.Supervision;

/// <summary>
/// Pure snapshot reducer. A pane whose status maps to null (unknown / no
/// agent) is dropped; a status-change for an untracked pane inserts it, so
/// event order relative to the initial agent.list does not matter.
/// </summary>
public static class SupervisionReducer
{
    public static HerdrSupervisionSnapshot Reduce(
        HerdrSupervisionSnapshot snapshot, SupervisionEvent evt) =>
        evt switch
        {
            EvtAgentsListed listed => WithSessions(snapshot, Resync(listed.Agents)),
            EvtPaneUpserted upserted => WithSessions(snapshot, Upsert(snapshot, upserted.Pane)),
            EvtAgentStatusChanged changed => WithSessions(snapshot, ApplyStatus(snapshot, changed)),
            EvtPaneRemoved removed => WithSessions(
                snapshot,
                snapshot.Sessions.Where(s => s.PaneId != removed.PaneId)),
            EvtConnectionFailed failed => snapshot with
            {
                ConnectionFailed = true,
                ConnectionFailureReason = failed.Reason,
            },
            EvtConnectionRestored => snapshot with
            {
                ConnectionFailed = false,
                ConnectionFailureReason = null,
            },
            _ => snapshot,
        };

    private static IEnumerable<AgentSessionSummary> Resync(IReadOnlyList<HerdrPaneAgent> agents) =>
        agents.Select(ToSession).OfType<AgentSessionSummary>();

    private static IEnumerable<AgentSessionSummary> Upsert(
        HerdrSupervisionSnapshot snapshot, HerdrPaneAgent pane)
    {
        var others = snapshot.Sessions.Where(s => s.PaneId != pane.PaneId);
        var mapped = ToSession(pane);
        return mapped is null ? others : others.Append(mapped);
    }

    private static IEnumerable<AgentSessionSummary> ApplyStatus(
        HerdrSupervisionSnapshot snapshot, EvtAgentStatusChanged changed)
    {
        var existing = snapshot.Sessions.FirstOrDefault(s => s.PaneId == changed.PaneId);
        var others = snapshot.Sessions.Where(s => s.PaneId != changed.PaneId);
        var mapped = HerdrStateMapper.Map(changed.Status);
        if (mapped is null)
            return others;
        var (phase, humanInput) = mapped.Value;
        var session = new AgentSessionSummary(
            changed.PaneId,
            changed.WorkspaceId,
            existing?.TabId ?? "",
            phase,
            humanInput,
            changed.Title ?? existing?.Title,
            changed.Agent ?? existing?.Agent);
        return others.Append(session);
    }

    private static AgentSessionSummary? ToSession(HerdrPaneAgent pane)
    {
        var mapped = HerdrStateMapper.Map(pane.AgentStatus);
        if (mapped is null)
            return null;
        var (phase, humanInput) = mapped.Value;
        return new AgentSessionSummary(
            pane.PaneId,
            pane.WorkspaceId,
            pane.TabId,
            phase,
            humanInput,
            pane.Title ?? pane.TerminalTitle,
            pane.DisplayAgent ?? pane.Agent);
    }

    private static HerdrSupervisionSnapshot WithSessions(
        HerdrSupervisionSnapshot snapshot, IEnumerable<AgentSessionSummary> sessions) =>
        snapshot with
        {
            Sessions = sessions
                .OrderBy(s => s.WorkspaceId, StringComparer.Ordinal)
                .ThenBy(s => s.PaneId, StringComparer.Ordinal)
                .ToList(),
        };
}
