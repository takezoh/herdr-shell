---
id: adr-20260908-one-shot-connection
kind: adr
title: One connection per request for the herdr socket API
status: accepted
created: '2026-09-08'
decision_makers:
- takezoh
consequences:
  positive:
  - Every request is isolated; a dropped connection faults exactly one call and nothing
    else.
  - No id-correlation map, no pending-request bookkeeping, no head-of-line blocking.
  - The fake endpoint reproduces the real server exactly, so tests cannot pass with
    a shape the server rejects.
  negative:
  - One connect/close per request (measured well under 10 ms on a local socket; acceptable
    for supervision-rate traffic).
  - A future herdr protocol that allows pipelining would leave this design slower
    than necessary until revisited.
  neutral:
  - Subscription and request paths are separate types (HerdrEventStream vs HerdrRequestClient)
    rather than one client.
confirmation: HerdrRequestClientTests.EveryRequestOpensItsOwnConnection and the FakeHerdrEndpoint
  one-shot behavior; re-verify against a real server with `HerdrShell.Probe ping2`
  after any herdr upgrade.
tags:
- herdr
- protocol
owners: []
relations:
- {type: references, target: design-herdr-shell-core}
source_paths:
- src/HerdrShell.Core/Client/HerdrRequestClient.cs
- src/HerdrShell.Core/Client/HerdrEventStream.cs
summary: herdr 0.8.2 closes every connection after one response, so the client is
  a per-request connector plus a separate events.subscribe stream instead of a multiplexer.
---

{% context %}
The first client implementation multiplexed requests over a single
connection with id correlation. Against herdr 0.8.2 every second request
on the same connection failed with a broken pipe: the server closes the
connection right after sending a response. Two sequential pings, a title
set followed by a clear, and a subscribe followed by agent.list all
reproduced it. The only connection that stays open is one that performed
`events.subscribe`, which then carries events only.
{% /context %}

{% decision %}
Treat the herdr socket API as one request per connection. `HerdrRequestClient`
connects, sends one request with id "1", reads its response, and disposes
the transport. `HerdrEventStream` owns the one long-lived connection
shape: it subscribes, confirms `subscription_started`, and then only
reads events. No component may send a request on an established stream
connection. The test fake (`FakeHerdrEndpoint`) closes after every
non-subscribe response so the test suite fails if this rule is broken.
{% /decision %}

{% consequence kind="positive" %}
Isolated requests, no correlation state, and a fake that mirrors reality.
{% /consequence %}

{% consequence kind="negative" %}
A connect/close per request; a future pipelining-capable protocol would
require revisiting this ADR.
{% /consequence %}

{% consequence kind="neutral" %}
Request and stream clients are distinct types instead of one multiplexer.
{% /consequence %}

## Confirmation

`HerdrRequestClientTests.EveryRequestOpensItsOwnConnection` asserts two
requests use two connections; `HerdrShell.Probe ping2` reproduces the
server's close-after-response on a live herdr.
