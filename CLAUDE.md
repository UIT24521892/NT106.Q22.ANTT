# CLAUDE.md

This file guides Claude Code when working in this repository. It is derived from
`PROJECT_SUMMARY.md` (the full analysis snapshot) — read that file for exhaustive
detail. This document is the quick operational reference.

## Project

**NT106 Monopoly Game** — a server-authoritative multiplayer Monopoly-style board
game for the NT106 (Network Programming) course. Human players, bots, rooms,
property transactions, cards, match timers, scoring, and Firebase-backed accounts.

- Primary solution: `MonopolyGame.sln`
- Active branch: `feature/logic_game`
- Status legend used throughout: ✅ Done | ⚠️ Partial | ❌ Not started

## Technology stack

| Layer | Technology |
|---|---|
| Main game client | Unity 2022.3.62f3, C#, UGUI, TextMesh Pro |
| Game server | .NET 8 console app, C#, `TcpListener`/`TcpClient` |
| Shared code | .NET 8 class library (packets, DTOs, board/card data, entities) |
| Authentication | Firebase Identity Toolkit REST API |
| Persistent data | Firebase Realtime Database REST API |
| Serialization | Newtonsoft.Json 13.0.4 |
| Legacy client | .NET 8 Windows Forms prototype (incomplete) |

## Architecture (three layers)

1. **Unity client** — login, lobby, board, player state, cards, chat, settings,
   property info, debt-sale selection, results. Sends player intentions; renders
   authoritative state updates.
2. **.NET TCP server** — owns rooms and gameplay state in memory. Validates
   actions, rolls dice, moves players, charges rent/tax, applies cards, runs bots
   and timers, handles bankruptcy, broadcasts state.
3. **Firebase REST** — authenticates users; persists profile, score, win/loss,
   match history, leaderboard. **Not** used for live gameplay sync.

`Monopoly.Shared` supplies packet models and static config to the .NET projects.
Unity mirrors network DTOs locally (`NetworkDataModels.cs`) instead of referencing
the shared assembly — these can drift; keep them in sync.

## Networking protocol

- Plain TCP, UTF-8 text. JSON envelope: `{ "Type": "PACKET_TYPE", "Payload": {} }`.
- Every frame ends with the literal delimiter `<EOF>`. Read buffers are 4096 bytes;
  both ends buffer partial reads.
- **Exception:** authentication uses a raw pipe-delimited response
  (`SUCCESS|...` / `FAIL|...`) read by a separate stream reader — a legacy split.
- The server router dispatches by **string packet name** (with aliases/casing
  variants), not the shared `PacketType` enum (which is stale/incomplete).
- Everything authoritative (dice, money, position, ownership, development, cards,
  debts, timers, results) is computed server-side; clients only request actions.

## Key conventions

- C# types/members `PascalCase`; private/locals `camelCase`.
- Namespaces: `Monopoly.Server.*`, `Monopoly.Shared.*`. Most Unity scripts use the
  global namespace.
- Methods suffixed `Unsafe` require the caller to already hold `ServerState.Lock`
  (this is a lock-ownership convention, NOT C# `unsafe` memory).
- Unity scene objects use prefixes `Panel_`, `Btn_`, `Txt_`, `Img_`.
- **Board transforms must stay `BoardPoint_00` … `BoardPoint_31`.** Do NOT reorder
  board entries or scene board points — token movement, tile-click mapping, state
  positions, and visual placement all share the same index.
- Singletons: `NetworkManager`, `AudioManager`, `ServiceLocator`.
- Runtime UI fallback pattern (`EnsureExists`): use authored prefab/scene object if
  present, else build a functional runtime UI.

## Canonical static data (do not fork)

- `Monopoly.Shared/Models/Configs/StaticData/BoardDatabase.cs` — 32-square board.
- `Monopoly.Shared/Models/Configs/StaticData/CardDatabase.cs` — card IDs/effects.

Key economy values (in source): starting money 2,000,000; pass-Start 300,000; turn
45s; match 10/20/30/60 min; tax 100,000; Lost Island exit 200,000; 3 houses before
hotel; scores 100/50/20/5.

## Build & verify

- `dotnet build MonopolyGame.sln` — succeeds, 0 errors, ~112–114 warnings (mostly
  nullable-reference). No automated test suite for gameplay/protocol exists beyond
  `GameEngineTests.cs`.
- Unity scenes/prefabs require Editor verification — not covered by the .NET build.
- Server defaults to TCP port `8080` (`--port` / `MONOPOLY_PORT`).
- Unity endpoint config: `Assets/StreamingAssets/server-config.json`, or
  `--server-host` / `--server-port` command line.

## Important known issues (be careful around these)

- **Security:** server logs can print raw passwords/Firebase tokens; Firebase API
  key & DB URL are hardcoded in source; no TLS; resume identifies by username not a
  validated UID/token.
- **State:** rooms/matches are in-memory only — server restart loses everything.
  The card deck is **global across all rooms** (shared 97-card deck).
- **Rules gaps:** building does not require a complete color set; human
  double/triple-double is less complete than bots; complete color-set rule ❌.
- **Firebase:** match-result updates are non-transactional REST read-modify-write
  (concurrent writes can clobber); no retry queue.
- **Unity:** Vietnamese mojibake / missing TMP glyphs in some files; many large
  UI surfaces are runtime-generated; tile art loaded by normalized resource name
  (missing names render blank).

## Working guidance

- When a change spans the protocol, keep server handler, shared DTO, and the Unity
  mirror DTO in sync within the same focused change; avoid unrelated scene/asset
  churn.
- Server entry: `Monopoly.Server/Program.cs`. Routing: `Network/PacketRouter.cs`.
  Core rules: `GameLogic/GameEngine.cs`. Firebase: `Services/FirebaseApiService.cs`.
- Unity network hub: `Assets/Scripts/NetworkManager.cs`. Scene order:
  `SampleScene` → `LobbyScene` → `GameScene`.

---
For the complete feature matrix, folder map, and full issue list, see
[`PROJECT_SUMMARY.md`](./PROJECT_SUMMARY.md).
