---
id: design-herdr-shell-core
kind: design
title: 'herdr-shell core: supervision surface over the herdr socket API'
status: active
created: '2026-09-08'
scope_type: component
responsibilities:
- id: RESP-001
  statement: Present the fleet of herdr agent panes as supervision sessions (phase,
    attention flag, title) derived only from herdr's semantic agent_status.
- id: RESP-002
  statement: Keep the supervision snapshot current through a long-lived events.subscribe
    stream, resynced from agent.list on every (re)connect.
- id: RESP-003
  statement: Jump the user to a session in two layers — agent.focus inside herdr,
    then raising the hosting terminal — and report which layer succeeded.
- id: RESP-004
  statement: Own the herdr wire contract for this repository; the captured API schema
    snapshot is the single source of truth that code and tests are pinned to.
invariants:
- id: INV-001
  statement: Exactly one request is sent per herdr connection; only a connection that
    performed events.subscribe stays open, and it never carries another request.
  enforcement: test
- id: INV-002
  statement: Every method name, subscription type, and enum value used by the client
    exists in schema/herdr-api-schema-v20.json with the same spelling.
  enforcement: contract
- id: INV-003
  statement: A title stamp placed on the hosting terminal (client.window_title.set)
    is always cleared before the jump returns, including when tab activation throws.
  enforcement: test
- id: INV-004
  statement: Jump-back never activates an arbitrary window; when the target cannot
    be identified the outcome degrades explicitly (ActivatedWindowOnly / HerdrFocusedOnly
    / NotFound).
  enforcement: test
- id: INV-005
  statement: Core has no dependency on WinUI, Win32, UIA, or the wezterm binary; those
    enter only through ITerminalWindowActivator, ITerminalTabActivator, and IWezTermCli.
  enforcement: review
- id: INV-006
  statement: The reducer is pure and deterministic — same snapshot plus same event
    yields the same snapshot, sessions ordered by (workspace_id, pane_id).
  enforcement: test
boundaries:
  provides:
  - id: PROV-001
    statement: IHerdrControl — typed request surface (ping, agent.list, agent.focus,
      client.window_title.set/clear).
  - id: PROV-002
    statement: HerdrSupervisionFeed / HerdrSupervisionSession — snapshot stream with
      reconnect.
  - id: PROV-003
    statement: JumpOrchestrator — two-layer jump with explicit outcome.
  consumes:
  - id: CONS-001
    statement: herdr socket API protocol 20 over a unix domain socket or Windows named
      pipe (newline-delimited JSON).
  - id: CONS-002
    statement: Host terminal activation primitives supplied by the Windows host through
      the JumpBack seams.
  forbidden:
  - id: FORB-001
    statement: Rendering or attaching to terminal content; herdr owns navigation and
      rendering.
  - id: FORB-002
    statement: Structured approve/deny flows — herdr exposes no such API; the shell
      shows blocked state and jumps.
  - id: FORB-003
    statement: Multiplexing several requests over one herdr connection.
variability:
  fixed:
  - id: FIX-001
    statement: Wire protocol shape (id/method/params request, id/result|error response,
      event/data envelope).
  - id: FIX-002
    statement: agent_status → SessionPhase mapping — blocked is the only status that
      raises attention.
  free:
  - id: FREE-001
    statement: Transport (unix socket, named pipe, WSL relay) — chosen per host via
      HerdrTransportFactory.
  - id: FREE-002
    statement: Terminal route (WindowsTerminal / WezTerm / GenericWindow) — chosen
      per host via TerminalHostConfig.
  - id: FREE-003
    statement: Reconnect backoff policy — injectable; default full-jitter capped at
      30s.
capabilities:
- id: cap:herdr-fleet-supervision
  uniqueness: global
- id: cap:herdr-jump-back
  uniqueness: per-platform
failure_responsibilities:
- id: FAIL-001
  statement: Loss of the event stream is surfaced as ConnectionFailed on the snapshot
    (sessions retained) and repaired by the session loop, not by consumers.
- id: FAIL-002
  statement: A rejected agent.focus yields NotFound and leaves the terminal untouched.
- id: FAIL-003
  statement: UIA or window activation failures degrade the jump outcome; they never
    propagate as exceptions to the caller.
trust_boundaries:
- id: TRUST-001
  statement: The herdr socket is a local, unauthenticated trust-on-access endpoint;
    the shell must not expose it beyond the local user.
compatibility_policies:
- id: COMPAT-001
  statement: Supported herdr protocol is pinned by the schema snapshot; a protocol
    bump requires refreshing the snapshot and re-running the contract tests before
    any client change.
tags:
- herdr
- supervision
- jump-back
owners: []
relations: []
source_paths:
- src/HerdrShell.Core
- schema/herdr-api-schema-v20.json
summary: 'Governing design for the UI-independent core: herdr wire client (one request
  per connection plus one event stream), deterministic supervision snapshot, and two-layer
  jump-back with explicit outcomes; the schema snapshot is the wire contract.'
---

## Purpose

herdr-shell is an external GUI for herdr: a Windows-native supervision
surface (tray, toast, jump-back) that never renders a terminal. This
design governs the UI-independent core — the wire client, the
supervision state model, and jump-back orchestration — and fixes the
rules that future changes must respect.

## Responsibilities

The core turns herdr's per-pane `agent_status` into a deterministic
fleet snapshot, keeps that snapshot live through the event stream, and
offers a two-layer jump (herdr focus, then terminal raise) with an
explicit outcome. It also owns the wire contract for this repository via
the schema snapshot.

## Boundaries

Provided: `IHerdrControl`, the supervision feed/session, and
`JumpOrchestrator`. Consumed: the herdr socket API (protocol 20) and
host-supplied activation primitives through the JumpBack seams.
Forbidden: terminal rendering, structured approvals, and request
multiplexing.

## Invariants

See frontmatter INV-001..006. INV-001 and INV-002 come directly from
measured server behavior and the captured schema; INV-003/004 encode the
jump-back safety rules inherited from agent-grid's staged JumpBack.

## Collaboration

The Windows host (not yet built) implements the seams and composes
`HerdrSupervisionSession` + `JumpOrchestrator` behind tray/toast UI.
`HerdrShell.Probe` is the T3 evidence tool against a real herdr.

## Failure Responsibility

Stream loss and activation failures are absorbed here and reported as
snapshot state or degraded outcomes (FAIL-001..003); consumers never see
transport exceptions.

## Variability

Transport, terminal route, and backoff are injectable (FREE-001..003);
wire shape and the status mapping are fixed (FIX-001..002).

## Conformance

`SchemaContractTests` pins INV-002; `HerdrRequestClientTests` /
`HerdrEventStreamTests` pin INV-001; `JumpOrchestratorTests` pin
INV-003/004; `SupervisionReducerTests` pin INV-006. The fake endpoint
reproduces the one-shot connection model so tests cannot pass against a
protocol shape the real server rejects.

## Related Decisions

- adr-20260908-one-shot-connection — why the client is per-request + stream
- adr-20260908-title-stamp-jump — why Windows Terminal is located by a stamped nonce
- adr-20260908-lifecycle-subscriptions — why fleet supervision uses pane lifecycle events
