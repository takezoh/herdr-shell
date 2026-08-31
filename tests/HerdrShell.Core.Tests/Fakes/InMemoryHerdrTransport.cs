using System.Threading.Channels;
using HerdrShell.Core.Transport;

namespace HerdrShell.Core.Tests.Fakes;

public sealed class InMemoryHerdrTransport : IHerdrTransport
{
    private readonly ChannelReader<string> _incoming;
    private readonly ChannelWriter<string> _outgoing;
    private readonly CancellationTokenSource _closed = new();

    private InMemoryHerdrTransport(ChannelReader<string> incoming, ChannelWriter<string> outgoing)
    {
        _incoming = incoming;
        _outgoing = outgoing;
    }

    public static (InMemoryHerdrTransport Client, InMemoryHerdrTransport Server) CreatePair()
    {
        var clientToServer = Channel.CreateUnbounded<string>();
        var serverToClient = Channel.CreateUnbounded<string>();
        return (
            new InMemoryHerdrTransport(serverToClient.Reader, clientToServer.Writer),
            new InMemoryHerdrTransport(clientToServer.Reader, serverToClient.Writer));
    }

    public async Task<string?> ReadLineAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _closed.Token);
        try
        {
            return await _incoming.ReadAsync(linked.Token).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
        catch (OperationCanceledException) when (_closed.IsCancellationRequested &&
                                                 !ct.IsCancellationRequested)
        {
            // Own side closed: end-of-stream, not caller cancellation.
            return null;
        }
    }

    public Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        if (!_outgoing.TryWrite(line))
            throw new IOException("transport closed");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _outgoing.TryComplete();
        _closed.Cancel();
        return ValueTask.CompletedTask;
    }
}
