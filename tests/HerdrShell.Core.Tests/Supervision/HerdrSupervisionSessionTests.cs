using HerdrShell.Core.Protocol;
using HerdrShell.Core.Supervision;
using HerdrShell.Core.Tests.Fakes;
using Xunit;

namespace HerdrShell.Core.Tests.Supervision;

public sealed class HerdrSupervisionSessionTests
{
    [Fact]
    public async Task RunOnce_SubscribesBeforeListingThenPumpsEvents()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        endpoint.Handle(HerdrMethods.AgentList, _ => new
        {
            type = "agent_list",
            agents = new object[]
            {
                new { pane_id = "p1", workspace_id = "w1", tab_id = "t1", agent_status = "working" },
            },
        });
        var feed = new HerdrSupervisionFeed();
        await using var session = new HerdrSupervisionSession(endpoint.Factory, feed);
        var attention = new TaskCompletionSource<HerdrSupervisionSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        feed.SnapshotChanged += snapshot =>
        {
            if (snapshot.AttentionCount > 0)
                attention.TrySetResult(snapshot);
        };

        var cycle = session.RunOnceAsync(Cts().Token);
        await WaitUntilAsync(() => feed.Snapshot.Sessions.Count == 1);

        Assert.Equal(
            [HerdrMethods.EventsSubscribe, HerdrMethods.AgentList],
            endpoint.Requests.Select(r => r.Method));
        Assert.False(feed.Snapshot.ConnectionFailed);

        await endpoint.SendEventAsync("pane.agent_status_changed", new
        {
            pane_id = "p1",
            workspace_id = "w1",
            agent_status = "blocked",
            title = "needs approval",
        });
        var observed = await attention.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var session1 = Assert.Single(observed.Sessions);
        Assert.True(session1.HumanInput);

        await endpoint.CloseStreamsAsync();
        await cycle.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RunLoop_MarksFailureAndRecoversOnReconnect()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        endpoint.Handle(HerdrMethods.AgentList, _ => new
        {
            type = "agent_list",
            agents = Array.Empty<object>(),
        });
        var feed = new HerdrSupervisionFeed();
        await using var session = new HerdrSupervisionSession(
            endpoint.Factory, feed, backoff: (_, _) => TimeSpan.FromMilliseconds(10));
        session.Start();
        await WaitUntilAsync(() => endpoint.Requests.Count >= 2);

        await endpoint.CloseStreamsAsync();

        await WaitUntilAsync(() => feed.Snapshot.ConnectionFailed);
        // Reconnect: a second subscribe/list pair restores the feed.
        await WaitUntilAsync(() => !feed.Snapshot.ConnectionFailed);
        Assert.True(endpoint.ConnectionCount >= 4);
    }

    private static CancellationTokenSource Cts() => new(TimeSpan.FromSeconds(5));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "timed out waiting for condition");
            await Task.Delay(10);
        }
    }
}
