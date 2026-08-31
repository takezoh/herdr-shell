using HerdrShell.Core.Client;
using HerdrShell.Core.Protocol;
using HerdrShell.Core.Tests.Fakes;
using Xunit;

namespace HerdrShell.Core.Tests.Client;

public sealed class HerdrEventStreamTests
{
    [Fact]
    public async Task Open_SendsTypedSubscriptionList()
    {
        await using var endpoint = new FakeHerdrEndpoint();

        await using var stream = await HerdrEventStream.OpenAsync(
            endpoint.Factory, ["pane.updated", "pane.closed"], Cts().Token);

        var request = Assert.Single(endpoint.Requests.ToArray());
        Assert.Equal(HerdrMethods.EventsSubscribe, request.Method);
        var subs = request.Params.GetProperty("subscriptions");
        Assert.Equal(2, subs.GetArrayLength());
        Assert.Equal("pane.updated", subs[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task Events_AreDeliveredNormalized()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        await using var stream = await HerdrEventStream.OpenAsync(
            endpoint.Factory, ["pane.agent_status_changed"], Cts().Token);
        var received = new TaskCompletionSource<HerdrEventEnvelope>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pump = stream.ReadAllAsync(envelope => received.TrySetResult(envelope), Cts().Token);

        await endpoint.SendEventAsync("pane.agent_status_changed", new
        {
            pane_id = "p1",
            workspace_id = "w1",
            agent_status = "blocked",
        });

        var envelope = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("pane_agent_status_changed", envelope.NormalizedEvent);
        Assert.Equal("p1", envelope.Data.GetProperty("pane_id").GetString());
        await endpoint.CloseStreamsAsync();
        await pump;
    }

    [Fact]
    public async Task ReadAll_ReturnsWhenServerClosesStream()
    {
        await using var endpoint = new FakeHerdrEndpoint();
        await using var stream = await HerdrEventStream.OpenAsync(
            endpoint.Factory, ["pane.updated"], Cts().Token);
        var pump = stream.ReadAllAsync(_ => { }, Cts().Token);

        await endpoint.CloseStreamsAsync();

        await pump.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static CancellationTokenSource Cts() => new(TimeSpan.FromSeconds(5));
}
