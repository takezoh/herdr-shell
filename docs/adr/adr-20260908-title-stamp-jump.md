---
id: adr-20260908-title-stamp-jump
kind: adr
title: Windows Terminal jump via title stamp and UIA tab selection
status: accepted
created: '2026-09-08'
decision_makers:
- takezoh
consequences:
  positive:
  - Background Windows Terminal tabs become addressable without any tab-enumeration
    API from WT itself.
  - The nonce is unique per jump, so tabs with similar user-visible titles cannot
    be confused.
  - Degradation is explicit; the worst case is window-level activation, never a wrong
    window.
  negative:
  - Depends on UIA reading the WT tab strip; UI tree churn or a profile with suppressApplicationTitle
    breaks the tab layer (falls back to window level).
  - Briefly rewrites the user's visible tab title during the jump.
  neutral:
  - WezTerm does not need this route; it is activated precisely through `wezterm cli
    activate-pane`.
confirmation: JumpOrchestratorTests cover happy path, no_foreground_client fallback,
  UIA exception fallback, and the clear-always rule; `HerdrShell.Probe title-probe`
  confirms stamp/clear on a live herdr.
tags:
- herdr
- jump-back
- windows-terminal
owners: []
relations:
- {type: references, target: design-herdr-shell-core}
source_paths:
- src/HerdrShell.Core/JumpBack/JumpOrchestrator.cs
summary: Windows Terminal tabs are located by stamping a unique nonce on the hosting
  title via client.window_title.set and selecting the matching TabItem through UIA,
  always clearing the stamp afterwards.
---

{% context %}
Windows Terminal exposes no public API to enumerate or focus tabs by
identity; `wt focus-tab` needs an index nobody outside WT can discover,
and the HWND title only reflects the active tab. herdr, however, offers
`client.window_title.set/clear`, which makes the attached foreground
client rewrite the hosting terminal's title via OSC — and measurement
showed it succeeds from an external process (`changed=true reason=set`).
UIA can read the names of background TabItems, so a unique title becomes
a locator.
{% /context %}

{% decision %}
For `TerminalHostKind.WindowsTerminal` the jump proceeds: `agent.focus`
(herdr layer) → `client.window_title.set(nonce)` → UIA select the
TabItem whose name contains the nonce → `SetForegroundWindow` on the
window now titled with the nonce → `client.window_title.clear`. The clear
runs in a `finally` and also when UIA throws. If the stamp returns
`no_foreground_client`, or the tab is not found, the jump degrades to
window-level activation by process name; the outcome enum states which
layer succeeded. WezTerm uses `wezterm cli activate-pane` with the pane
id inherited from `WEZTERM_PANE` and does not stamp titles.
{% /decision %}

{% consequence kind="positive" %}
Background WT tabs are reachable; nonce uniqueness prevents misfires;
degradation is explicit.
{% /consequence %}

{% consequence kind="negative" %}
UIA dependence and a transient visible title change; a WT profile with
`suppressApplicationTitle` disables the tab layer.
{% /consequence %}

{% consequence kind="neutral" %}
WezTerm takes a different, exact route and is unaffected by this ADR.
{% /consequence %}

## Confirmation

`JumpOrchestratorTests.WindowsTerminal_*` pin the sequence and the
clear-always rule; `HerdrShell.Probe title-probe` reproduces the stamp
and clear on a live herdr (verified 2026-08-31 against 0.8.2).
