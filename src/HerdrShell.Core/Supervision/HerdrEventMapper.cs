using System.Text.Json;
using HerdrShell.Core.Client;
using HerdrShell.Core.Protocol;

namespace HerdrShell.Core.Supervision;

/// <summary>
/// Translates broadcast/subscription envelopes into reducer events.
/// Unrecognized envelopes map to null and are ignored by the feed.
/// </summary>
public static class HerdrEventMapper
{
    public static SupervisionEvent? TryMap(HerdrEventEnvelope envelope)
    {
        switch (envelope.NormalizedEvent)
        {
            case "pane_created":
            case "pane_updated":
            {
                if (!envelope.Data.TryGetProperty("pane", out var paneProp))
                    return null;
                var pane = paneProp.Deserialize<HerdrPaneAgent>(HerdrJson.Options);
                return pane is null ? null : new EvtPaneUpserted(pane);
            }
            case "pane_closed":
            case "pane_exited":
            {
                var paneId = GetString(envelope.Data, "pane_id");
                return paneId is null ? null : new EvtPaneRemoved(paneId);
            }
            case "pane_agent_status_changed":
            {
                var paneId = GetString(envelope.Data, "pane_id");
                var workspaceId = GetString(envelope.Data, "workspace_id");
                if (paneId is null || workspaceId is null)
                    return null;
                if (!envelope.Data.TryGetProperty("agent_status", out var statusProp))
                    return null;
                var status = statusProp.Deserialize<HerdrAgentStatus>(HerdrJson.Options);
                return new EvtAgentStatusChanged(
                    paneId,
                    workspaceId,
                    status,
                    GetString(envelope.Data, "title"),
                    GetString(envelope.Data, "agent"));
            }
            default:
                return null;
        }
    }

    private static string? GetString(JsonElement data, string name) =>
        data.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
