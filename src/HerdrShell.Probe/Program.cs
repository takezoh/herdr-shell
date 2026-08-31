using HerdrShell.Core.Client;
using HerdrShell.Core.Supervision;
using HerdrShell.Core.Transport;

// Real-socket probe for the herdr backend. Commands:
//   ping | agents | watch [seconds] | focus <target>
//   title-probe [seconds] | title-clear
// Socket path: --socket <path> (default: ~/.config/herdr/herdr.sock).
// `title-probe` stamps a marker on the hosting terminal's title, reports the
// reason, waits, then clears — the T3 evidence run for the Windows Terminal
// jump route. `watch` proves the event stream stays open and maps statuses.

var socketPath = HerdrEndpoint.DefaultUnixSocketPath();
var args2 = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--socket" && i + 1 < args.Length)
        socketPath = args[++i];
    else
        args2.Add(args[i]);
}

if (args2.Count == 0)
{
    Console.Error.WriteLine(
        "usage: probe [--socket <path>] <ping|agents|watch [sec]|focus <target>|title-probe [sec]|title-clear>");
    return 2;
}

HerdrTransportFactory connect = ct => HerdrEndpoint.ConnectUnixAsync(socketPath, ct);
var control = new HerdrRequestClient(connect);

switch (args2[0])
{
    case "ping":
    {
        var pong = await control.PingAsync();
        Console.WriteLine($"protocol={pong.Protocol} version={pong.Version}");
        return 0;
    }
    case "agents":
    {
        foreach (var agent in await control.ListAgentsAsync())
        {
            Console.WriteLine(
                $"{agent.PaneId} ws={agent.WorkspaceId} tab={agent.TabId} " +
                $"status={agent.AgentStatus} agent={agent.DisplayAgent ?? agent.Agent} " +
                $"title={agent.Title ?? agent.TerminalTitle}");
        }
        return 0;
    }
    case "watch":
    {
        var seconds = args2.Count > 1 ? int.Parse(args2[1]) : 0;
        var feed = new HerdrSupervisionFeed();
        feed.SnapshotChanged += snapshot =>
        {
            Console.WriteLine(
                $"[{DateTimeOffset.Now:HH:mm:ss}] sessions={snapshot.Sessions.Count} " +
                $"attention={snapshot.AttentionCount} failed={snapshot.ConnectionFailed}");
            foreach (var session in snapshot.Sessions)
            {
                Console.WriteLine(
                    $"  {session.PaneId} {session.Phase}{(session.HumanInput ? "!" : "")} {session.Title}");
            }
        };
        await using var session2 = new HerdrSupervisionSession(connect, feed);
        using var cts = seconds > 0
            ? new CancellationTokenSource(TimeSpan.FromSeconds(seconds))
            : new CancellationTokenSource();
        Console.WriteLine(seconds > 0 ? $"watching for {seconds}s" : "watching (ctrl-c to stop)");
        try
        {
            await session2.RunOnceAsync(cts.Token);
            Console.WriteLine("stream closed by server");
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("stream still open at deadline (healthy)");
            return 0;
        }
    }
    case "focus" when args2.Count > 1:
    {
        await control.FocusAgentAsync(args2[1]);
        Console.WriteLine("focused");
        return 0;
    }
    case "title-probe":
    {
        var holdSeconds = args2.Count > 1 ? int.Parse(args2[1]) : 5;
        var marker = $"herdr-shell-probe-{Environment.ProcessId}";
        var set = await control.SetClientWindowTitleAsync(marker);
        Console.WriteLine($"set: changed={set.Changed} reason={set.Reason} marker={marker}");
        if (set.Changed)
        {
            Console.WriteLine($"holding {holdSeconds}s — check the terminal tab title now");
            await Task.Delay(TimeSpan.FromSeconds(holdSeconds));
            var clear = await control.ClearClientWindowTitleAsync();
            Console.WriteLine($"clear: changed={clear.Changed} reason={clear.Reason}");
        }
        return set.Changed ? 0 : 1;
    }
    case "title-clear":
    {
        var clear = await control.ClearClientWindowTitleAsync();
        Console.WriteLine($"clear: changed={clear.Changed} reason={clear.Reason}");
        return 0;
    }
    default:
        Console.Error.WriteLine($"unknown command: {args2[0]}");
        return 2;
}
