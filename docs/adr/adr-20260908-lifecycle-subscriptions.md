---
id: adr-20260908-lifecycle-subscriptions
kind: adr
title: Fleet supervision via pane lifecycle subscriptions
status: accepted
created: '2026-09-08'
decision_makers:
- takezoh
consequences:
  positive:
  - One subscription set covers every pane, present and future, with no per-pane bookkeeping
    or resubscription on pane creation.
  - pane_updated carries the full PaneInfo, so the reducer can always upsert from
    complete data.
  negative:
  - Redraw-heavy panes emit many no-op pane_updated events; the feed must deduplicate
    identical snapshots (done) and consumers must debounce transient `unknown` (open
    issue).
  neutral:
  - The per-pane pane.agent_status_changed subscription remains available for a future
    focused-pane detail view.
confirmation: SchemaContractTests.EveryFeedSubscriptionExistsInSchema pins the subscription
  set; `HerdrShell.Probe watch 12` shows live transitions across all agents.
tags:
- herdr
- supervision
owners: []
relations:
- {type: references, target: design-herdr-shell-core}
source_paths:
- src/HerdrShell.Core/Supervision/HerdrSupervisionFeed.cs
- src/HerdrShell.Core/Supervision/HerdrEventMapper.cs
summary: Fleet supervision subscribes to pane lifecycle events (pane.updated carries
  agent_status) because pane.agent_status_changed is a per-pane filter requiring pane_id.
---

{% context %}
The obvious subscription for status tracking, `pane.agent_status_changed`,
was rejected by the live server with `invalid request: missing field
pane_id`: the schema marks it as a per-pane filter (`pane_id` required),
as are `pane.output_matched` and `pane.scroll_changed`. Fleet-wide
supervision needs every pane, including ones created after subscribing.
{% /context %}

{% decision %}
`HerdrSupervisionFeed.RequiredSubscriptions` is the lifecycle set
`pane.created`, `pane.updated`, `pane.closed`, `pane.exited`,
`pane.agent_detected`. `pane_updated` events carry the full PaneInfo
(including `agent_status`), which the reducer upserts; `pane_closed` /
`pane_exited` remove. The session opens the stream first and then calls
`agent.list` so the resync reflects everything ordered before it. The
mapper still understands `pane_agent_status_changed` payloads for a
future per-pane mode. Identical consecutive snapshots are suppressed in
the feed because redraw churn produces many no-op updates.
{% /decision %}

{% consequence kind="positive" %}
Complete coverage of current and future panes from one subscription set,
with full-projection upserts.
{% /consequence %}

{% consequence kind="negative" %}
Event volume is high under redraw; dedupe is in place, debounce of
transient `unknown` is still owed before notifications use the feed.
{% /consequence %}

{% consequence kind="neutral" %}
Per-pane subscriptions stay available for a detail view.
{% /consequence %}

## Confirmation

`SchemaContractTests.EveryFeedSubscriptionExistsInSchema` pins the set to
the schema; `HerdrShell.Probe watch 12` observed Running/Waiting/Done
transitions on live agents (2026-08-31, herdr 0.8.2).
