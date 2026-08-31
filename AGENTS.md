# Build & Test

.NET 8。ローカルに dotnet が無ければ `mise exec dotnet@8 -- dotnet ...`。

```sh
dotnet build HerdrShell.sln
dotnet test HerdrShell.sln                                  # T1: 常時全部通す
dotnet run --project src/HerdrShell.Probe -- ping           # T3: 実 herdr socket 検証
```

# Rules

- **契約の正本は `schema/herdr-api-schema-v20.json`** (herdr api schema --json の
  スナップショット)。メソッド・enum・購読名を増やすときは
  `tests/.../Contract/SchemaContractTests.cs` に必ず追記する。protocol を上げる
  ときはスナップショットを取り直す
- **接続モデルを壊さない**: herdr socket API は 1 接続 1 リクエスト
  (実測済み・fake も同挙動)。multiplexing 前提のコードを書かない。
  ストリームは `events.subscribe` した接続だけ
- fake (`tests/.../Fakes/FakeHerdrEndpoint.cs`) と実サーバーの挙動が食い違ったら
  **fake を直す** (assertion を弱めない)。実挙動の確認は HerdrShell.Probe
- UI 依存・Win32/UIA 依存は seam (interface) の背後に置き、Core は
  Linux/WSL でテスト可能に保つ (agent-grid Windows Shell と同じ規律)
- 新機能・バグ修正はテストと一緒に。テスト無しで完了扱いしない
