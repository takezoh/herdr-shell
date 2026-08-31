# herdr-shell

[herdr](https://herdr.dev/) の外部 GUI(Windows ネイティブ supervision surface)。
agent-grid の Windows Shell(tray / toast / deep link / jump-back)のアーキテクチャを
herdr socket API に向けて再構成したもの。ターミナルは描画しない —
herdr が server-side mux としてナビゲーションを担い、この shell は
監視・通知・「該当セッションへ跳ぶ」だけを提供する。

## 動くもの (現状)

- **HerdrShell.Core** — UI 非依存の中核 (Linux/WSL/Windows でビルド・テスト可)
  - `Protocol/` — herdr socket API (protocol 20) のワイヤ型。正本は
    `schema/herdr-api-schema-v20.json` (`herdr api schema --json` のスナップショット)
    で、`SchemaContractTests` が同期を強制する
  - `Client/` — `HerdrRequestClient` (1 接続 1 リクエスト) と
    `HerdrEventStream` (events.subscribe ストリーム)
  - `Supervision/` — herdr agent_status → SessionPhase の写像、純粋 reducer、
    再接続バックオフ付き `HerdrSupervisionSession`
  - `JumpBack/` — 2 層 jump (herdr 内 `agent.focus` + ターミナル前面化) の
    オーケストレーション。Windows Terminal は `client.window_title.set` で
    nonce をタブタイトルに刻印 → UIA で TabItem 特定 → SetForegroundWindow →
    刻印を必ず clear。WezTerm は `wezterm cli activate-pane`。
    Win32/UIA/wezterm は seam (interface) の背後で、実装は Windows 側の宿題
- **HerdrShell.Probe** — 実 socket 検証 CLI
  (`ping | agents | watch [sec] | focus <target> | title-probe [sec] | title-clear`)
- **HerdrShell.Core.Tests** — 48 tests。fake は実測済みの接続モデル
  (one-shot + subscribe ストリーム) を忠実に再現する

## 実測で確定した herdr 0.8.2 の挙動

- socket API は **1 接続 1 リクエスト**。応答直後にサーバーが切断する。
  唯一の例外が `events.subscribe` で、接続はイベントストリームになる
  (以降のリクエストは受け付けない)
- `client.window_title.set` は **外部プロセスからでも成功する**
  (`changed=true reason=set`)。attach 中の foreground クライアントが OSC で
  ホストターミナルのタブタイトルを書き換える。WT jump 路線の前提は成立
- `pane.agent_status_changed` 購読は per-pane (`pane_id` 必須)。fleet 全体の
  supervision は lifecycle 購読 (`pane.updated` が agent_status 込みの
  PaneInfo を運ぶ) で構成する

## ビルド / テスト

.NET 8 SDK (このマシンでは `mise exec dotnet@8 -- dotnet ...`):

```sh
dotnet build HerdrShell.sln
dotnet test HerdrShell.sln
dotnet run --project src/HerdrShell.Probe -- ping        # 実 socket 検証
dotnet run --project src/HerdrShell.Probe -- watch 12    # ライブ supervision
```

## 未実装 / 既知課題

- **Windows ホスト** (WinUI tray/toast/panel + 実 UIA `ITerminalTabActivator` +
  実 `ITerminalWindowActivator` + named pipe endpoint)。Core の seam は用意済み。
  Windows での herdr named pipe 名は未確認 — 実機で要検証
- **検出フラッピング**: herdr の screen-detection が再描画中に一時的に
  `unknown` を返し、セッションが消えて戻る (実測)。tray/toast 側に
  debounce (猶予時間) を入れてから通知に使うこと
- **WSL 越しの接続**: herdr が WSL 内で動く場合、Windows 側 shell からは
  `wsl.exe` 経由の実行か中継が必要 (AF_UNIX の WSL2 相互運用はない)
- 承認 (approve/deny) の構造化往復は herdr に存在しない。`blocked` 状態の
  表示 + jump が v1 のスコープ
