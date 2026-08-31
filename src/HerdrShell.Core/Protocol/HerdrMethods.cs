namespace HerdrShell.Core.Protocol;

/// <summary>
/// herdr socket API method names (protocol 20).
/// Contract snapshot: schema/herdr-api-schema-v20.json — kept in sync by
/// SchemaContractTests; add the method to the schema assertion when extending.
/// </summary>
public static class HerdrMethods
{
    public const string Ping = "ping";
    public const string AgentList = "agent.list";
    public const string AgentFocus = "agent.focus";
    public const string EventsSubscribe = "events.subscribe";
    public const string ClientWindowTitleSet = "client.window_title.set";
    public const string ClientWindowTitleClear = "client.window_title.clear";
}

/// <summary>
/// events.subscribe subscription type constants (dotted form). Broadcast
/// envelopes arrive with the underscore form of the same names.
/// pane.agent_status_changed is a per-pane filter (requires pane_id —
/// verified against herdr 0.8.2); fleet-wide supervision listens to the
/// lifecycle types instead, where pane.updated carries the full PaneInfo
/// including agent_status.
/// </summary>
public static class HerdrSubscriptions
{
    public const string PaneCreated = "pane.created";
    public const string PaneUpdated = "pane.updated";
    public const string PaneClosed = "pane.closed";
    public const string PaneExited = "pane.exited";
    public const string PaneAgentDetected = "pane.agent_detected";
}
