using HerdrShell.Core.Protocol;
using HerdrShell.Core.Supervision;
using Xunit;

namespace HerdrShell.Core.Tests.Supervision;

public sealed class SupervisionReducerTests
{
    private static HerdrPaneAgent Pane(
        string paneId,
        HerdrAgentStatus status = HerdrAgentStatus.Working,
        string workspace = "w1",
        string? title = null) =>
        new(paneId, workspace, "t1", status, Agent: "claude", Title: title);

    [Fact]
    public void AgentsListed_ReplacesTrackedSessions()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty,
            new EvtAgentsListed([Pane("p1"), Pane("p2", HerdrAgentStatus.Blocked)]));

        snapshot = SupervisionReducer.Reduce(
            snapshot, new EvtAgentsListed([Pane("p3", HerdrAgentStatus.Idle)]));

        var session = Assert.Single(snapshot.Sessions);
        Assert.Equal("p3", session.PaneId);
        Assert.Equal(SessionPhase.Waiting, session.Phase);
    }

    [Fact]
    public void AgentsListed_DropsUnknownStatusPanes()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty,
            new EvtAgentsListed([Pane("p1"), Pane("p2", HerdrAgentStatus.Unknown)]));

        Assert.Single(snapshot.Sessions);
    }

    [Fact]
    public void StatusChanged_InsertsUntrackedPane()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty,
            new EvtAgentStatusChanged("p9", "w2", HerdrAgentStatus.Blocked, "review diff", "codex"));

        var session = Assert.Single(snapshot.Sessions);
        Assert.Equal("p9", session.PaneId);
        Assert.True(session.HumanInput);
        Assert.Equal("review diff", session.Title);
    }

    [Fact]
    public void StatusChanged_PreservesTitleWhenEventOmitsIt()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty,
            new EvtAgentsListed([Pane("p1", title: "fix login")]));

        snapshot = SupervisionReducer.Reduce(
            snapshot,
            new EvtAgentStatusChanged("p1", "w1", HerdrAgentStatus.Blocked, null, null));

        var session = Assert.Single(snapshot.Sessions);
        Assert.Equal("fix login", session.Title);
        Assert.Equal(SessionPhase.Waiting, session.Phase);
    }

    [Fact]
    public void StatusChanged_ToUnknownRemovesSession()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty, new EvtAgentsListed([Pane("p1")]));

        snapshot = SupervisionReducer.Reduce(
            snapshot,
            new EvtAgentStatusChanged("p1", "w1", HerdrAgentStatus.Unknown, null, null));

        Assert.Empty(snapshot.Sessions);
    }

    [Fact]
    public void PaneRemoved_DropsOnlyThatSession()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty,
            new EvtAgentsListed([Pane("p1"), Pane("p2")]));

        snapshot = SupervisionReducer.Reduce(snapshot, new EvtPaneRemoved("p1"));

        var session = Assert.Single(snapshot.Sessions);
        Assert.Equal("p2", session.PaneId);
    }

    [Fact]
    public void PaneUpserted_UpdatesExistingSession()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty, new EvtAgentsListed([Pane("p1")]));

        snapshot = SupervisionReducer.Reduce(
            snapshot,
            new EvtPaneUpserted(Pane("p1", HerdrAgentStatus.Done, title: "shipped")));

        var session = Assert.Single(snapshot.Sessions);
        Assert.Equal(SessionPhase.Done, session.Phase);
        Assert.Equal("shipped", session.Title);
    }

    [Fact]
    public void ConnectionFailure_KeepsSessionsAndClearsOnRestore()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty, new EvtAgentsListed([Pane("p1")]));

        snapshot = SupervisionReducer.Reduce(snapshot, new EvtConnectionFailed("socket gone"));
        Assert.True(snapshot.ConnectionFailed);
        Assert.Equal("socket gone", snapshot.ConnectionFailureReason);
        Assert.Single(snapshot.Sessions);

        snapshot = SupervisionReducer.Reduce(snapshot, new EvtConnectionRestored());
        Assert.False(snapshot.ConnectionFailed);
        Assert.Null(snapshot.ConnectionFailureReason);
    }

    [Fact]
    public void Sessions_OrderIsDeterministic()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty,
            new EvtAgentsListed(
                [Pane("pb", workspace: "w2"), Pane("pa", workspace: "w1"), Pane("pc", workspace: "w1")]));

        Assert.Equal(["pa", "pc", "pb"], snapshot.Sessions.Select(s => s.PaneId));
    }

    [Fact]
    public void AttentionCount_CountsBlockedOnly()
    {
        var snapshot = SupervisionReducer.Reduce(
            HerdrSupervisionSnapshot.Empty,
            new EvtAgentsListed(
            [
                Pane("p1", HerdrAgentStatus.Blocked),
                Pane("p2", HerdrAgentStatus.Working),
                Pane("p3", HerdrAgentStatus.Idle),
            ]));

        Assert.Equal(1, snapshot.AttentionCount);
    }
}
