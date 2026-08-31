namespace HerdrShell.Core.Transport;

/// <summary>
/// Newline-delimited JSON duplex link to a herdr server. Implementations:
/// unix domain socket (Linux/WSL), named pipe (Windows), in-memory (tests).
/// </summary>
public interface IHerdrTransport : IAsyncDisposable
{
    /// <summary>Next line, or null at end-of-stream.</summary>
    Task<string?> ReadLineAsync(CancellationToken ct = default);

    Task WriteLineAsync(string line, CancellationToken ct = default);
}
