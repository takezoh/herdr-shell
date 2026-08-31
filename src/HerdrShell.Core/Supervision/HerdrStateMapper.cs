using HerdrShell.Core.Protocol;

namespace HerdrShell.Core.Supervision;

public static class HerdrStateMapper
{
    /// <summary>
    /// herdr agent_status → shell phase. Returns null for panes the shell
    /// does not supervise (no detected agent). "blocked" is herdr's
    /// needs-human-input state and is the only phase that raises attention.
    /// </summary>
    public static (SessionPhase Phase, bool HumanInput)? Map(HerdrAgentStatus status) =>
        status switch
        {
            HerdrAgentStatus.Working => (SessionPhase.Running, false),
            HerdrAgentStatus.Blocked => (SessionPhase.Waiting, true),
            HerdrAgentStatus.Idle => (SessionPhase.Waiting, false),
            HerdrAgentStatus.Done => (SessionPhase.Done, false),
            HerdrAgentStatus.Unknown => null,
            _ => null,
        };
}
