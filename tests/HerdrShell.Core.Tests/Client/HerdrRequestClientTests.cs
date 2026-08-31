using HerdrShell.Core.Client;
using HerdrShell.Core.Protocol;
using HerdrShell.Core.Tests.Fakes;
using Xunit;

namespace HerdrShell.Core.Tests.Client;

public sealed class HerdrRequestClientTests
{
    [Fact]
    public async Task Ping_RoundtripsProtocolVersion()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        endpoint.Handle(HerdrMethods.Ping, _ => new { type = "pong", protocol = 20, version = "0.8.2" });
        var client = new HerdrRequestClient(endpoint.Factory);

        var pong = await client.PingAsync(Cts().Token);

        Assert.Equal(20, pong.Protocol);
        Assert.Equal("0.8.2", pong.Version);
    }

    [Fact]
    public async Task EveryRequestOpensItsOwnConnection()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        endpoint.Handle(HerdrMethods.Ping, _ => new { type = "pong", protocol = 20 });
        var client = new HerdrRequestClient(endpoint.Factory);

        await client.PingAsync(Cts().Token);
        await client.PingAsync(Cts().Token);

        Assert.Equal(2, endpoint.ConnectionCount);
    }

    [Fact]
    public async Task ListAgents_ParsesSnakeCasePayload()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        endpoint.Handle(HerdrMethods.AgentList, _ => new
        {
            type = "agent_list",
            agents = new object[]
            {
                new
                {
                    pane_id = "p1",
                    workspace_id = "w1",
                    tab_id = "t1",
                    agent_status = "blocked",
                    agent = "claude",
                    display_agent = "Claude Code",
                    title = "fix tests",
                    focused = true,
                },
            },
        });
        var client = new HerdrRequestClient(endpoint.Factory);

        var agents = await client.ListAgentsAsync(Cts().Token);

        var agent = Assert.Single(agents);
        Assert.Equal("p1", agent.PaneId);
        Assert.Equal(HerdrAgentStatus.Blocked, agent.AgentStatus);
        Assert.Equal("Claude Code", agent.DisplayAgent);
        Assert.True(agent.Focused);
    }

    [Fact]
    public async Task ErrorResponse_SurfacesCodeAndMessage()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        endpoint.Handle(HerdrMethods.AgentFocus, _ => new FakeHerdrError("not_found", "no such agent"));
        var client = new HerdrRequestClient(endpoint.Factory);

        var ex = await Assert.ThrowsAsync<HerdrApiException>(
            () => client.FocusAgentAsync("ghost", Cts().Token));

        Assert.Equal("not_found", ex.Code);
        Assert.Equal("no such agent", ex.Message);
    }

    [Fact]
    public async Task WindowTitleSet_ParsesNoForegroundClientReason()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        endpoint.Handle(HerdrMethods.ClientWindowTitleSet, _ => new
        {
            type = "client_window_title",
            changed = false,
            reason = "no_foreground_client",
        });
        var client = new HerdrRequestClient(endpoint.Factory);

        var result = await client.SetClientWindowTitleAsync("nonce", Cts().Token);

        Assert.False(result.Changed);
        Assert.Equal(ClientWindowTitleReason.NoForegroundClient, result.Reason);
    }

    [Fact]
    public async Task ConnectionClosedWithoutResponse_ThrowsNamingTheMethod()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        // No handler registered: the fake closes without responding.
        var client = new HerdrRequestClient(endpoint.Factory);

        var ex = await Assert.ThrowsAsync<IOException>(
            () => client.FocusAgentAsync("p1", Cts().Token));

        Assert.Contains(HerdrMethods.AgentFocus, ex.Message);
    }

    private static CancellationTokenSource Cts() => new(TimeSpan.FromSeconds(5));
}
