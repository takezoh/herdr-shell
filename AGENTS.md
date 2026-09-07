# Build & Test

.NET 8. If dotnet is not on PATH, use `mise exec dotnet@8 -- dotnet ...`.

```sh
dotnet build HerdrShell.sln
dotnet test HerdrShell.sln                                  # T1: always keep green
dotnet run --project src/HerdrShell.Probe -- ping           # T3: verify against the real herdr socket
```

# Rules

- **The contract source of truth is `schema/herdr-api-schema-v20.json`**
  (a snapshot of `herdr api schema --json`). Whenever you add a method,
  enum, or subscription name, extend
  `tests/.../Contract/SchemaContractTests.cs` accordingly. When bumping the
  supported protocol, refresh the snapshot
- **Do not break the connection model**: the herdr socket API serves one
  request per connection (verified by measurement; the fake behaves the
  same). Never write code that assumes multiplexing. Only a connection that
  performed `events.subscribe` stays open, as an event stream
- If the fake (`tests/.../Fakes/FakeHerdrEndpoint.cs`) and the real server
  disagree, **fix the fake** (do not weaken assertions). Verify real
  behavior with HerdrShell.Probe
- Keep UI and Win32/UIA dependencies behind seams (interfaces) so Core
  stays testable on Linux/WSL (the same discipline as agent-grid's Windows
  Shell)
- New features and bug fixes ship with tests. Work is not complete without
  them

# Documentation (dev-docs)

Structured docs live under `docs/` (Markdown + YAML frontmatter, managed by the
dev-docs CLI). `docs/design/design-herdr-shell-core.md` is the governing design
(responsibilities, invariants, seams); `docs/adr/` holds the decisions that bind
future changes; `docs/note/` holds measurements and research logs. Update
frontmatter, lifecycle, and relations through the CLI, not by hand, and run
`docs lint` after writing. A change that alters a responsibility, boundary, or
invariant must update the design through a change package's promotion, not by
editing the design directly.
