using HerdrShell.Core.Client;

namespace HerdrShell.Core.Supervision;

/// <summary>
/// Open event stream → resync via agent.list → pump events; on stream close
/// the feed is marked failed and the loop reconnects with full-jitter
/// backoff (capped), mirroring the agent-grid SupervisionWsSession pattern.
/// </summary>
public sealed class HerdrSupervisionSession : IAsyncDisposable
{
    private readonly HerdrTransportFactory _connect;
    private readonly HerdrSupervisionFeed _feed;
    private readonly TimeSpan _maxBackoff;
    private readonly Func<int, TimeSpan, TimeSpan> _backoff;
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _loopTask;

    public HerdrSupervisionSession(
        HerdrTransportFactory connect,
        HerdrSupervisionFeed feed,
        TimeSpan? maxBackoff = null,
        Func<int, TimeSpan, TimeSpan>? backoff = null)
    {
        _connect = connect;
        _feed = feed;
        _maxBackoff = maxBackoff ?? TimeSpan.FromSeconds(30);
        _backoff = backoff ?? FullJitter;
    }

    public HerdrSupervisionFeed Feed => _feed;

    public void Start()
    {
        if (_loopTask is not null)
            return;
        _loopTask = Task.Run(() => RunLoopAsync(_lifetime.Token));
    }

    /// <summary>Single subscribe→resync→pump cycle; returns when the stream closes.</summary>
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        await using var stream = await HerdrEventStream
            .OpenAsync(_connect, HerdrSupervisionFeed.RequiredSubscriptions, ct)
            .ConfigureAwait(false);
        var control = new HerdrRequestClient(_connect);
        var agents = await control.ListAgentsAsync(ct).ConfigureAwait(false);
        _feed.Apply(new EvtAgentsListed(agents));
        _feed.Apply(new EvtConnectionRestored());
        await stream.ReadAllAsync(_feed.OnEnvelope, ct).ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(ct).ConfigureAwait(false);
                _feed.Apply(new EvtConnectionFailed("herdr event stream closed"));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _feed.Apply(new EvtConnectionFailed(ex.Message));
            }
            attempt++;
            try
            {
                await Task.Delay(_backoff(attempt, _maxBackoff), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static TimeSpan FullJitter(int attempt, TimeSpan max)
    {
        var cappedExponent = Math.Min(attempt, 6);
        var ceiling = Math.Min(
            TimeSpan.FromSeconds(Math.Pow(2, cappedExponent)).TotalMilliseconds,
            max.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * ceiling);
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        if (_loopTask is not null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // expected on shutdown
            }
        }
        _lifetime.Dispose();
    }
}
