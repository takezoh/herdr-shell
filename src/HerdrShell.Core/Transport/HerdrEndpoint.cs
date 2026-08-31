using System.IO.Pipes;
using System.Net.Sockets;

namespace HerdrShell.Core.Transport;

/// <summary>
/// Socket-path resolution and transport connection.
/// Unix: ~/.config/herdr/herdr.sock, named sessions under
/// ~/.config/herdr/sessions/&lt;name&gt;/herdr.sock.
/// Windows: named pipe (name must currently be supplied by config; the
/// default pipe name is not documented — verify against a native Windows
/// herdr before shipping that path).
/// </summary>
public static class HerdrEndpoint
{
    public static string DefaultUnixSocketPath(string? sessionName = null, string? configHome = null)
    {
        configHome ??= Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(configHome))
        {
            configHome = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        }
        var root = Path.Combine(configHome, "herdr");
        return sessionName is null
            ? Path.Combine(root, "herdr.sock")
            : Path.Combine(root, "sessions", sessionName, "herdr.sock");
    }

    public static async Task<IHerdrTransport> ConnectUnixAsync(
        string socketPath, CancellationToken ct = default)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), ct)
                .ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
        return new StreamHerdrTransport(new NetworkStream(socket, ownsSocket: true));
    }

    public static async Task<IHerdrTransport> ConnectNamedPipeAsync(
        string pipeName, CancellationToken ct = default)
    {
        var pipe = new NamedPipeClientStream(
            ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await pipe.DisposeAsync().ConfigureAwait(false);
            throw;
        }
        return new StreamHerdrTransport(pipe);
    }
}
