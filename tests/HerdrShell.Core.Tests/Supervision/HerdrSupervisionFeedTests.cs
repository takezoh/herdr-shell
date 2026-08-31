using System.Text.Json;
using HerdrShell.Core.Client;
using HerdrShell.Core.Protocol;
using HerdrShell.Core.Supervision;
using Xunit;

namespace HerdrShell.Core.Tests.Supervision;

public sealed class HerdrSupervisionFeedTests
{
    [Fact]
    public void Envelope_StatusChangeUpdatesSnapshotAndRaisesEvent()
    {
        var feed = new HerdrSupervisionFeed();
        HerdrSupervisionSnapshot? observed = null;
        feed.SnapshotChanged += snapshot => observed = snapshot;
        var data = JsonSerializer.SerializeToElement(new
        {
            pane_id = "p1",
            workspace_id = "w1",
            agent_status = "blocked",
            title = "needs approval",
        }, HerdrJson.Options);

        feed.OnEnvelope(new HerdrEventEnvelope("pane.agent_status_changed", data));

        Assert.NotNull(observed);
        var session = Assert.Single(observed.Sessions);
        Assert.True(session.HumanInput);
        Assert.Equal("needs approval", session.Title);
    }

    [Fact]
    public void Envelope_PaneUpdatedUpsertsFromFullProjection()
    {
        var feed = new HerdrSupervisionFeed();
        var data = JsonSerializer.SerializeToElement(new
        {
            pane = new
            {
                pane_id = "p1",
                workspace_id = "w1",
                tab_id = "t1",
                agent_status = "working",
                title = "build feature",
            },
        }, HerdrJson.Options);

        feed.OnEnvelope(new HerdrEventEnvelope("pane_updated", data));

        var session = Assert.Single(feed.Snapshot.Sessions);
        Assert.Equal(SessionPhase.Running, session.Phase);
        Assert.Equal("build feature", session.Title);
    }

    [Fact]
    public void Apply_NoOpEventDoesNotRepublishSnapshot()
    {
        var feed = new HerdrSupervisionFeed();
        var pane = new HerdrPaneAgent("p1", "w1", "t1", HerdrAgentStatus.Working, Title: "x");
        feed.Apply(new EvtPaneUpserted(pane));
        var published = 0;
        feed.SnapshotChanged += _ => published++;

        feed.Apply(new EvtPaneUpserted(pane));

        Assert.Equal(0, published);
    }

    [Fact]
    public void Envelope_UnrecognizedEventIsIgnored()
    {
        var feed = new HerdrSupervisionFeed();
        var raised = false;
        feed.SnapshotChanged += _ => raised = true;
        var data = JsonSerializer.SerializeToElement(new { workspace_id = "w1" });

        feed.OnEnvelope(new HerdrEventEnvelope("workspace_focused", data));

        Assert.False(raised);
        Assert.Empty(feed.Snapshot.Sessions);
    }
}
