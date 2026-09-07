---
id: note-20260908-herdr-082-measurements
kind: note
title: Measured behavior of herdr 0.8.2 socket API
status: published
created: '2026-09-08'
summary: Research log of what a live herdr 0.8.2 server actually does, captured with
  HerdrShell.Probe on 2026-08-31; these observations drove three ADRs.
tags:
- herdr
- research
owners: []
relations:
- {type: references, target: adr-20260908-one-shot-connection}
- {type: references, target: adr-20260908-title-stamp-jump}
- {type: references, target: adr-20260908-lifecycle-subscriptions}
source_paths:
- src/HerdrShell.Probe/Program.cs
- schema/herdr-api-schema-v20.json
---

## Summary

All observations below were taken with `HerdrShell.Probe` against a
locally running herdr 0.8.2 (protocol 20) in WSL on 2026-08-31, with two
live agents (one codex, one claude) attached in the user's terminal.
They are facts about the server, not about this repository, and should
be re-measured after any herdr upgrade.

## Notes

### Connection lifecycle

- `ping` on a fresh connection: `protocol=20 version=0.8.2`.
- `ping2` (two pings, one connection): the second write fails with
  `Broken pipe`. Same for `client.window_title.set` → `clear`, and for
  `events.subscribe` → `agent.list`. The server closes after every
  response except a successful subscribe. → adr-20260908-one-shot-connection
- `watch 12` (subscribe stream held 12 s): the stream stayed open and
  delivered events continuously; no idle timeout observed at that scale.

### Title stamping

- `client.window_title.set` from an external, non-attached process:
  `changed=True reason=Set`. The stamped marker appeared on the user's
  terminal tab; `clear` on a fresh connection returned
  `changed=True reason=Cleared`. → adr-20260908-title-stamp-jump
- The `no_foreground_client` reason was not triggered while a client was
  attached; its exact trigger (no attached client? detached session?) is
  still unmeasured.

### Subscriptions

- `pane.agent_status_changed` without `pane_id`:
  `invalid request: missing field pane_id` — it is a per-pane filter.
  → adr-20260908-lifecycle-subscriptions
- Lifecycle set (`pane.created/updated/closed/exited/agent_detected`)
  subscribed fine and `pane_updated` payloads carried `agent_status`.

### Detection behavior

- Screen-detection flapping: during heavy redraw the claude pane briefly
  reported `unknown` and dropped out of the fleet list, then returned
  within about a second; the codex pane cycled Running → Done → Running
  several times a second and its title alternated between two values.
  Snapshot dedupe was added in response; a debounce before tray/toast
  notifications is still required.

### agent.list shape

- Returned `{type: "agent_list", agents: [AgentInfo...]}` with
  `pane_id`, `workspace_id`, `tab_id`, `agent_status`, `agent`,
  `display_agent`, `title`, `focused` — matching the captured schema and
  parsed by the snake_case options without per-field overrides.
