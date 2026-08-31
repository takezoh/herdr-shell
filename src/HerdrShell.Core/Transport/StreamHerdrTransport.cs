using System.Text;

namespace HerdrShell.Core.Transport;

public sealed class StreamHerdrTransport : IHerdrTransport
{
    private readonly Stream _stream;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public StreamHerdrTransport(Stream stream)
    {
        _stream = stream;
        _reader = new StreamReader(stream, new UTF8Encoding(false), leaveOpen: true);
        _writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true)
        {
            NewLine = "\n",
            AutoFlush = true,
        };
    }

    public Task<string?> ReadLineAsync(CancellationToken ct = default) =>
        _reader.ReadLineAsync(ct).AsTask();

    public async Task WriteLineAsync(string line, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _writeGate.Dispose();
        _reader.Dispose();
        await _writer.DisposeAsync().ConfigureAwait(false);
        await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
