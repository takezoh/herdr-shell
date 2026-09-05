# herdr-shell

An external GUI for [herdr](https://herdr.dev/) — a Windows-native supervision
surface. It reuses the architecture of agent-grid's Windows Shell
(tray / toast / deep links / jump-back), pointed at the herdr socket API.
It does not render a terminal: herdr, as a server-side multiplexer, owns
navigation, and this shell only provides monitoring, notifications, and
"jump to the session that needs me".

## What works today

- **HerdrShell.Core** — UI-independent core (builds and tests on Linux/WSL/Windows)
  - `Protocol/` — wire types for the herdr socket API (protocol 20). The
    source of truth is `schema/herdr-api-schema-v20.json` (a snapshot of
    `herdr api schema --json`), kept in sync by `SchemaContractTests`
  - `Client/` — `HerdrRequestClient` (one request per connection) and
    `HerdrEventStream` (the events.subscribe stream)
  - `Supervision/` — herdr agent_status → SessionPhase mapping, a pure
    reducer, and `HerdrSupervisionSession` with reconnect backoff
  - `JumpBack/` — orchestration of the two-layer jump (`agent.focus` inside
    herdr + raising the hosting terminal). For Windows Terminal it stamps a
    nonce onto the tab title via `client.window_title.set`, locates the
    TabItem through UIA, calls SetForegroundWindow, and always clears the
    stamp. For WezTerm it uses `wezterm cli activate-pane`. Win32/UIA/wezterm
    live behind seams (interfaces); the real implementations are Windows-side
    work
- **HerdrShell.Probe** — real-socket verification CLI
  (`ping | agents | watch [sec] | focus <target> | title-probe [sec] | title-clear`)
- **HerdrShell.Core.Tests** — 48 tests. The fake reproduces the verified
  connection model (one-shot requests + subscribe stream) faithfully

## Behavior of herdr 0.8.2 confirmed by measurement

- The socket API serves **one request per connection**: the server closes
  the connection right after responding. The only exception is
  `events.subscribe`, which turns the connection into an event stream
  (no further requests are accepted on it)
- `client.window_title.set` **succeeds even from an external process**
  (`changed=true reason=set`). The foreground attached client rewrites the
  hosting terminal's tab title via OSC, so the Windows Terminal jump route
  is viable
- The `pane.agent_status_changed` subscription is per-pane (`pane_id`
  required). Fleet-wide supervision is built on the lifecycle subscriptions
  instead (`pane.updated` carries the full PaneInfo including agent_status)

## Build / test

.NET 8 SDK (on this machine: `mise exec dotnet@8 -- dotnet ...`):

```sh
dotnet build HerdrShell.sln
dotnet test HerdrShell.sln
dotnet run --project src/HerdrShell.Probe -- ping        # verify against the real socket
dotnet run --project src/HerdrShell.Probe -- watch 12    # live supervision
```

## Not implemented / known issues

- **Windows host** (WinUI tray/toast/panel, the real UIA
  `ITerminalTabActivator`, the real `ITerminalWindowActivator`, and the
  named pipe endpoint). The Core seams are in place. The herdr named pipe
  name on Windows is unverified — check against a native Windows herdr
- **Detection flapping**: herdr's screen detection transiently reports
  `unknown` during heavy redraws, making a session disappear and reappear
  (observed live). Add a debounce (grace period) before wiring this into
  tray/toast notifications
- **Connecting across WSL**: when herdr runs inside WSL, a Windows-side
  shell needs either `wsl.exe`-mediated execution or a relay (there is no
  AF_UNIX interop with WSL2)
- herdr has no structured approve/deny round trip. Showing the `blocked`
  state plus jump-back is the v1 scope
