using HerdrShell.Core.Client;
using HerdrShell.Core.Protocol;

namespace HerdrShell.Core.Supervision;

/// <summary>
/// Serializes reducer application and publishes snapshots. Wiring order is
/// owned by HerdrSupervisionSession: the event stream is opened before the
/// agent.list resync, so the list reply reflects everything ordered before
/// it and later events land on top — no transition is lost.
/// </summary>
public sealed class HerdrSupervisionFeed
{
    public static readonly IReadOnlyList<string> RequiredSubscriptions =
    [
        HerdrSubscriptions.PaneCreated,
        HerdrSubscriptions.PaneUpdated,
        HerdrSubscriptions.PaneClosed,
        HerdrSubscriptions.PaneExited,
        HerdrSubscriptions.PaneAgentDetected,
    ];

    private readonly object _gate = new();

    public HerdrSupervisionSnapshot Snapshot { get; private set; } = HerdrSupervisionSnapshot.Empty;

    public event Action<HerdrSupervisionSnapshot>? SnapshotChanged;

    public void Apply(SupervisionEvent evt)
    {
        HerdrSupervisionSnapshot next;
        lock (_gate)
        {
            var current = Snapshot;
            next = SupervisionReducer.Reduce(current, evt);
            // herdr redraw churn produces many no-op pane_updated events;
            // deduplicate so tray/toast consumers only see real changes.
            if (SameSnapshot(current, next))
                return;
            Snapshot = next;
        }
        SnapshotChanged?.Invoke(next);
    }

    private static bool SameSnapshot(
        HerdrSupervisionSnapshot a, HerdrSupervisionSnapshot b) =>
        a.ConnectionFailed == b.ConnectionFailed &&
        a.ConnectionFailureReason == b.ConnectionFailureReason &&
        a.Sessions.SequenceEqual(b.Sessions);

    public void OnEnvelope(HerdrEventEnvelope envelope)
    {
        var evt = HerdrEventMapper.TryMap(envelope);
        if (evt is not null)
            Apply(evt);
    }
}
