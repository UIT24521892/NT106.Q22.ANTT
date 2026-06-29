# NT106 Monopoly Game — Project Summary

> Analysis snapshot: June 7, 2026  
> Repository branch inspected: `feature/logic_game`  
> Primary solution: `MonopolyGame.sln`  
> Status legend: ✅ Done | ⚠️ Partial | ❌ Not started

## 1. Project Overview

### Project identity

- **Project:** NT106 Monopoly Game
- **Course:** NT106 - Network Programming
- **Purpose:** A server-authoritative multiplayer Monopoly-style board game supporting human players, bots, rooms, property transactions, cards, match timers, scoring, and Firebase-backed accounts.

### Technology stack

| Layer | Technology |
|---|---|
| Main game client | Unity 2022.3.62f3, C#, UGUI, TextMesh Pro |
| Game server | .NET 8 console application, C#, `TcpListener`/`TcpClient` |
| Shared code | .NET 8 class library containing packets, DTOs, board data, card data, and entities |
| Authentication | Firebase Identity Toolkit REST API |
| Persistent user data | Firebase Realtime Database REST API |
| Serialization | Newtonsoft.Json |
| Legacy client | .NET 8 Windows Forms prototype |

### Architecture overview

The active system has three main layers:

1. **Unity client:** Displays login, lobby, board, player state, cards, chat, settings, property information, debt-sale selection, and match results. It sends player intentions to the server and renders authoritative state updates.
2. **.NET TCP server:** Owns rooms and gameplay state in memory. It validates actions, rolls dice, moves players, charges rent/tax, applies cards, runs bots and timers, handles bankruptcy, and broadcasts state.
3. **Firebase REST services:** Authenticate users and persist profile, score, win/loss, match history, and leaderboard data. Firebase is not used for live gameplay synchronization.

`Monopoly.Shared` supplies common packet models and static game configuration to the .NET projects. Unity mirrors several network DTOs locally rather than directly referencing the .NET shared assembly.

### Multiplayer model

- Clients connect to one TCP server endpoint.
- The server listens on all interfaces and defaults to port `8080`.
- The endpoint can be configured through:
  - Server: `--port` or `MONOPOLY_PORT`.
  - Unity client: `Assets/StreamingAssets/server-config.json`.
  - Unity command line: `--server-host` and `--server-port`.
- A connection authenticates with Firebase and is then associated with a UID, username, token, and optional room ID.
- Rooms support 2-4 total slots, including bots.
- The room host configures map metadata, bot count, maximum players, and match duration.
- Live rooms and game states are stored in static server memory guarded by `ServerState.Lock`.
- The server broadcasts `GAME_STATE_UPDATE` packets after authoritative changes.
- Disconnect/resume is supported only while the same server process and room remain alive.
- Public Internet play is technically configurable, but deployment, TLS, DNS, firewall, and production hosting are not included in the repository.

## 2. Features Implemented

### Lobby system

| Feature | Status | Notes |
|---|---|---|
| TCP server connection | ✅ Done | Unity connects to a configurable host and port. |
| Create room | ✅ Done | Host creates a random four-digit room ID. |
| Room list | ✅ Done | Server returns waiting rooms that are not full or started. |
| Join room | ✅ Done | Validates missing room, duplicate player, full room, and game status. |
| Player slots | ✅ Done | Lobby displays human and bot slots. |
| Maximum players | ✅ Done | Configurable from 2 to 4. |
| Ready state | ✅ Done | Host is automatically ready; other humans toggle ready; bots are ready. |
| Host-only start | ✅ Done | Start requires host authority, at least two total players, and all required players ready. |
| Bots in lobby | ✅ Done | Supports 0-3 bots within room capacity. |
| Match duration selection | ✅ Done | Accepted values are 10, 20, 30, or 60 minutes. |
| Map selection | ⚠️ Partial | Map name is carried by room data and shown in UI, but does not select different board rules/assets. |
| Leave waiting room | ✅ Done | Non-host leaves normally; host leaving closes the room. |
| Logout | ✅ Done | Client session and room association are cleared. |
| Reconnect/resume | ⚠️ Partial | Finds an active in-memory room by username and reconnects the player. No persisted session, UID verification, or restart recovery. |
| LAN play | ✅ Done | Other machines can connect to the host's LAN IP when firewall and port allow it. |
| Public Internet play | ⚠️ Partial | Endpoint configuration and deployment documentation exist; no hosted production server is supplied. |

### Gameplay

| Feature | Status | Notes |
|---|---|---|
| 32-square board database | ✅ Done | Static board order, type, price, rent, and color data are defined centrally. |
| Server-authoritative dice | ✅ Done | Human rolls use cryptographic random generation; bot rolls use server random generation. |
| Token movement synchronization | ✅ Done | Uses `LastMoveFromPosition`, `LastMoveToPosition`, and `LastFinalPosition`. |
| Pass Start reward | ✅ Done | Awards 200,000 when crossing Start. |
| Turn timer | ✅ Done | Default turn duration is 45 seconds; timeout advances the turn. |
| Match countdown | ✅ Done | Server ends matches at configured 10/20/30/60-minute duration. |
| Property purchase | ✅ Done | City and resort squares can be bought if unowned and affordable. |
| Rent payment | ✅ Done | Rent is based on property level and active card effects. |
| Build houses | ✅ Done | City properties support three houses; during the first round each city can be built up to one house only. |
| Build hotel | ✅ Done | Hotel follows three houses. |
| Complete color-set rule | ❌ Not started | Current build validation does not require ownership of the complete color group. |
| Property marker visualization | ⚠️ Partial | Runtime markers now load `Resources/UI/house` and `Resources/UI/hotel` sprites (lightly tinted by owner) when present, and fall back to colored UI bars when the sprite assets are missing. |
| Property detail popup | ⚠️ Partial | Large runtime title-deed popup, pricing, rent table, image slot, and upgrade action exist; design is still code-generated and resource-name dependent. |
| Tile image loading | ⚠️ Partial | City images load from `Resources/TileImages`; resort and missing-name assets fall back to blank. |
| Tax squares | ✅ Done | Fixed debt is charged through the common debt system. |
| Resort squares | ✅ Done | Includes Hawaii, Nice, Dubai, Cyprus, and Bali. |
| Lost Island/jail behavior | ✅ Done | Player attempts doubles and eventually pays an exit fee. |
| World Tour | ✅ Done | Human players choose a destination; choosing a lower board index counts as passing Start and awards the Start bonus. Bots still apply skip-turn behavior. |
| World Championship | ✅ Done | Charges opponents and supports movement through a card effect. |
| Chance card draw | ✅ Done | Global deck contains 97 generated card copies and reshuffles when exhausted. |
| Immediate card effects | ✅ Done | Fine, jackpot, jail, skip turn, tax penalty, charity, and world-tour effects are implemented. |
| Held card inventory | ✅ Done | Player hand is synchronized and shown as usable buttons. |
| Use held card | ✅ Done | Server validates ownership and applies supported effects. |
| Card target selection | ✅ Done | Server requests a target; owning client highlights valid board squares and returns a choice. |
| Card cancellation | ✅ Done | Optional target selection can be cancelled. |
| Free rent | ✅ Done | Prevents the next applicable rent payment. |
| Forced double | ✅ Done | Held-card effect exists; rolling doubles now grants an extra roll and three consecutive doubles sends the player straight to Lost Island, for both humans and bots. |
| Flight | ✅ Done | Allows movement to a selected board square. |
| Free upgrade | ✅ Done | Upgrades a valid owned city. |
| Earthquake | ✅ Done | Damages a selected developed opponent city. |
| Power outage | ✅ Done | Temporarily affects a target property. |
| Escape Island | ✅ Done | Releases a held player. |
| Chat | ✅ Done | Room chat, 160-character server limit, client history, and token bubbles are implemented. |
| Bot turns | ✅ Done | Bots roll, buy, build, sell, use cards, and finish turns automatically. |
| Bot/human rules parity | ⚠️ Partial | Bot logic contains separate decision/debt paths and does not always share the exact human action flow. |
| Pause request and voting | ✅ Done | Connected active humans vote; timers are shifted while paused. |
| Resume paused game | ⚠️ Partial | Any active player can resume gameplay without a second vote. |
| Surrender | ✅ Done | Surrender is handled as bankruptcy. |
| Audio settings | ✅ Done | Volume, music, effects, mute, and PlayerPrefs exist; SFX are wired to dice, buy, build, card draw, game over, UI click, and token jump, with background music on entering GameScene. Missing clips log a warning once. |

### Debt, property sale, and bankruptcy

| Feature | Status | Notes |
|---|---|---|
| Pay debt from cash | ✅ Done | Rent, tax, card penalties, and other charges first use available money. |
| Detect insufficient cash | ✅ Done | Server compares debt against cash and liquidation value. |
| Human property-sale selection | ✅ Done | Turn pauses and the affected player selects owned assets to liquidate. |
| Bot automatic sale | ✅ Done | Bots select and sell property automatically. |
| Reduced sale value | ✅ Done | Liquidation returns less than original purchase/development cost. |
| Immediate debt settlement | ✅ Done | Sale proceeds pay the pending debt first; remaining value returns to cash. |
| Property reset after sale | ✅ Done | Ownership, houses, hotel, and temporary property flags are cleared. |
| Bankruptcy | ✅ Done | Triggered when cash plus sellable assets cannot cover the debt. |
| Bankruptcy popup | ✅ Done | Full-screen queued event UI is available. |
| Creditor settlement | ✅ Done | Debt context retains the recipient for rent and other applicable transfers. |

### Game over and scoring

| Feature | Status | Notes |
|---|---|---|
| `GAME_OVER` packet | ✅ Done | Includes winner, reason, rankings, and score results. |
| Bankruptcy-based finish | ✅ Done | Match finishes when the active-player condition is reached. |
| Time-limit finish | ✅ Done | Players are ranked by net worth at match timeout. |
| Disconnect-based finish | ⚠️ Partial | Two-player disconnects can end a match before a resume is possible. |
| Ranking calculation | ✅ Done | Uses final placement; timeout ranking uses cash plus liquidation value. |
| Score rewards | ✅ Done | Rank rewards are 100, 50, 20, then 5 points. |
| Human-only persisted rankings | ✅ Done | Bots are excluded from Firebase match rewards. |
| Game-over UI | ✅ Done | Runtime ranking, leaderboard, and return-to-lobby flow exist. |
| End reason reporting | ✅ Done | Timeout, monopoly victories, last-player-standing, "all players left/bankrupt", and opponent-disconnect paths all set an explicit `EndReason`, which is now included in the `GAME_OVER` packet and shown on the result screen (default "Trận đấu kết thúc" if still blank). |
| Post-match cleanup | ⚠️ Partial | UI returns to lobby, but finished rooms are not consistently removed immediately from server memory. |

### Leaderboard

| Feature | Status | Notes |
|---|---|---|
| Firebase score persistence | ✅ Done | Match points, wins, losses, and profile metadata are updated. |
| Match history | ✅ Done | Per-match rank, earned score, and timestamp are written under the user. |
| Top leaderboard query | ✅ Done | Reads users by points and returns the top ten after local sorting. |
| Leaderboard packet | ✅ Done | `GET_LEADERBOARD` and `LEADERBOARD_DATA` are handled. |
| Game-over leaderboard display | ✅ Done | Results UI requests and displays leaderboard data. |
| Lobby leaderboard | ❌ Not started | No complete lobby leaderboard screen was found. |
| Atomic result updates | ❌ Not started | Current REST read-modify-write can lose concurrent increments. |
| Retry/offline queue | ❌ Not started | Failed Firebase result writes are not durably retried. |

### Authentication and profile

| Feature | Status | Notes |
|---|---|---|
| Firebase email/password login | ✅ Done | Uses Identity Toolkit `signInWithPassword`. |
| Firebase email/password registration | ✅ Done | Uses Identity Toolkit `signUp`. |
| Initial user profile creation | ✅ Done | Creates username, avatar, money, score, win/loss, and timestamps. |
| Profile update | ✅ Done | Username and avatar can be patched through the server. |
| Session storage in Unity | ✅ Done | UID, token, username, avatar, and score are stored in `PlayerSession`. |
| Password reset | ❌ Not started | A stale packet enum mentions it, but no implemented route or UI flow exists. |
| Secure token/identity validation | ⚠️ Partial | Firebase validates login, but profile/action identity checks are inconsistent after authentication. |
| Secret management | ❌ Not started | Firebase API configuration is hardcoded in source instead of environment/secret storage. |

### Unity UI and scenes

| Feature | Status | Notes |
|---|---|---|
| Splash/intro flow | ✅ Done | Present in `SampleScene`. |
| Login/register panels | ✅ Done | Email/password forms and scene transitions exist. |
| Lobby main menu | ✅ Done | Room creation, room browser, waiting room, profile, resume, and logout UI exist. |
| Four-corner player panels | ✅ Done | Existing scene panels are populated by `PlayerInfoLayerUI`. |
| Center dice UI | ✅ Done | Two dice sprites, total text, animation, and match timer are supported. |
| Board token animation | ✅ Done | Tokens animate through board point transforms. |
| Property popup | ⚠️ Partial | Functionally rich but generated at runtime and still being visually refined. |
| Chance card popup | ⚠️ Partial | Runtime chance-card popup now uses a tile-card style with a header that cycles Gold/Silver/Wood colors before revealing the drawn card's actual type; final authored prefab/art is still pending. |
| Full-screen event popup | ✅ Done | Queues bankruptcy, debt, card, loss, and error messages. |
| Property sale UI | ✅ Done | Full-screen selectable property list for debt settlement. |
| Chat prefab/runtime fallback | ✅ Done | Prefab is preferred with scene/runtime fallback. |
| Settings panel | ⚠️ Partial | Functional runtime panel and optional prefab loading exist; no tracked final settings prefab was found. |
| Game over panel | ⚠️ Partial | Functional runtime UI exists; final authored prefab/polish is pending. |
| Vietnamese font support | ⚠️ Partial | Vietnamese text is used, but TMP glyph/fallback warnings and mojibake remain. |
| Responsive resolution | ⚠️ Partial | Target is 1920x1080 windowed; many runtime layouts use fixed pixel values. |

## 3. Architecture & Technical Decisions

### Networking

#### Transport and framing

- Transport is plain TCP using `TcpListener`, `TcpClient`, and `NetworkStream`.
- Messages are UTF-8 text.
- JSON packets use this logical envelope:

```json
{
  "Type": "PACKET_TYPE",
  "Payload": {}
}
```

- Every frame ends with the literal delimiter `<EOF>`.
- Both client and server retain pending text buffers so fragmented and combined TCP reads can be reconstructed.
- Read buffers are 4096 bytes.
- `PacketHelper.GetPayloadObject` accepts both a JSON object and a JSON-encoded payload string for compatibility.

Authentication is an exception: its response is a raw pipe-delimited string such as `SUCCESS|...` or `FAIL|...`, while normal gameplay traffic uses JSON. This is a legacy protocol split.

#### Packet routing

The server router dispatches string packet names rather than relying on the shared `PacketType` enum. It accepts some aliases and casing variants.

Major client-to-server messages:

```text
Login, Register, UPDATE_PROFILE
CREATE_ROOM, GET_ROOM_LIST, JOIN_ROOM, PLAYER_READY, START_GAME
LEAVE_ROOM, LOGOUT, RESUME_GAME
ROLL_DICE/DiceRoll, END_TURN/EndTurn
BUY_PROPERTY/BuyProperty, BUILD_PROPERTY/BuildProperty
SELL_PROPERTY_FOR_DEBT
USE_CARD/UseCard, CARD_CHOICE_MADE
REQUEST_PAUSE, PAUSE_VOTE, RESUME_GAMEPLAY, SURRENDER_GAME
GAME_CHAT/CHAT_MESSAGE
GET_LEADERBOARD
```

Major server-to-client messages:

```text
ROOM_CREATED, CREATE_ROOM_FAILED, ROOM_LIST_RESPONSE
JOIN_ROOM_SUCCESS, JOIN_ROOM_FAILED, ROOM_UPDATE, ROOM_CLOSED
START_GAME_FAILED, GAME_STARTING, GAME_STATE_UPDATE
RESUME_GAME_NONE
CARD_DRAWN, REQUEST_CARD_CHOICE
CHAT_MESSAGE
GAME_ACTION_FAILED
GAME_OVER, LEADERBOARD_DATA
LEAVE_ROOM_SUCCESS
SUCCESS_PROFILE, FAIL_PROFILE
```

#### Authority and synchronization

- Dice, money, position, ownership, development, cards, debts, timers, and match results are server-authoritative.
- Clients send requested actions, not final state.
- After a mutation, the server generally broadcasts the complete public game state.
- `ServerState.Lock` serializes access to global room and connection dictionaries.
- Methods suffixed `Unsafe` expect the caller to already hold that lock; the suffix does not refer to C# unsafe memory.
- Network broadcasts iterate room connections sequentially.
- Failed sends are often ignored to keep the game loop moving.

#### Network limitations

- No TLS or application-level encryption.
- No packet length prefix or maximum frame-size enforcement.
- No heartbeat/keepalive protocol.
- No protocol version negotiation.
- No rate limiting or anti-spam controls.
- No durable reconnect token.
- No authoritative mapping between every action payload username and the authenticated UID.
- Server logs currently risk printing full incoming packets, including authentication credentials or tokens.

### Firebase

Firebase is accessed directly by server-side `HttpClient`; no Firebase SDK is used.

#### Authentication endpoints

- Email/password sign-up.
- Email/password sign-in.
- Firebase returns UID and ID token.
- The server then reads or creates the matching Realtime Database profile.

#### Realtime Database schema

Configuration values are intentionally omitted here. The source currently contains a project-specific API key and database base URL that should be moved to environment variables.

```text
USERS/
  {uid}/
    Username
    Email
    AvatarId
    Money
    Point
    TotalWins
    TotalLosses
    CreatedAt
    UpdatedAt
    MatchHistory/
      {matchId}/
        Rank
        ScoreEarned
        PlayedAt
```

- Initial profile money is 2,000,000.
- Leaderboard is based primarily on `Point`, then wins.
- Match result updates read the existing profile and PATCH incremented totals.
- The update is not a Firebase transaction, so concurrent writes can overwrite one another.
- Live rooms and game states are not persisted to Firebase.

### Unity client

#### Scene order

1. `SampleScene` - splash, intro, login, and registration.
2. `LobbyScene` - room creation, browser, waiting room, profile, and resume.
3. `GameScene` - board, tokens, actions, popups, chat, settings, and result flow.

#### Key runtime design

- `NetworkManager` is a `DontDestroyOnLoad` singleton and central network event hub.
- `AudioManager` is also a persistent singleton.
- `GameSceneUIBinder` attaches scene buttons and ensures optional UI modules exist.
- Many UI modules use an `EnsureExists` pattern:
  1. Use an authored scene object or prefab if available.
  2. Otherwise create a functional runtime UI.
- This fallback keeps features operational but produces large UI scripts and fixed-pixel layouts.
- Game state models are mirrored in `NetworkDataModels.cs`.
- Board movement depends on the exact `BoardPoint_00` through `BoardPoint_31` transform order.
- `Application.runInBackground` is enabled so animation and incoming state continue when Unity loses focus.

### .NET server

#### Connection lifecycle

1. `Program` starts the TCP listener and one-second `TurnTimer`.
2. `TcpServer` accepts each client and reads framed packets.
3. `PacketRouter` invokes a domain handler.
4. Authentication populates the `ClientConnection`.
5. Room handlers update waiting-room state.
6. Game handlers invoke `GameEngine`.
7. `NetworkSender` sends a direct response or broadcasts room state.
8. Disconnect handling marks active game players offline or removes waiting-room players.

#### State model

- `ServerState.Clients`: active TCP connections.
- `ServerState.Rooms`: waiting and active rooms.
- `Room`: host, slots, bots, options, and optional `GameState`.
- `GameState`: turn, timers, movement, cards, pause, debt, properties, and players.
- `GamePlayerState`: identity, cash, position, flags, cards, and connection status.
- `GamePropertyState`: ownership, houses, hotel, and temporary effects.

#### Timers and bots

- `TurnTimer` runs once per second.
- It advances timed-out turns and ends expired matches.
- It skips automatic advancement while a human must select a card target or sell property.
- Bot actions are queued with delayed tasks so they are visible to clients.
- Pausing shifts both turn and match deadlines.

## 4. Folder & File Structure

```text
Project/
├── MonopolyGame.sln
│   └── Solution for Shared, Server, and legacy WinForms client.
├── Monopoly.Server/
│   ├── Program.cs
│   │   └── Server entry point, port configuration, listener, and timer startup.
│   ├── Network/
│   │   ├── TcpServer.cs
│   │   │   └── Accept loop, TCP framing, receive loop, and disconnect handling.
│   │   ├── PacketRouter.cs
│   │   │   └── String-based packet dispatch and pause-state action guard.
│   │   └── NetworkSender.cs
│   │       └── Direct sends and room broadcasts.
│   ├── Handles/
│   │   ├── AuthHandler.cs
│   │   │   └── Firebase login, registration, and connection identity setup.
│   │   ├── RoomHandler.cs
│   │   │   └── Create/list/join/ready/start/chat lobby operations.
│   │   ├── GameHandler.cs
│   │   │   └── Dice, turn, purchase, build, leave, resume, and card orchestration.
│   │   ├── GameCardHandler.cs
│   │   │   └── Card use and target-selection flow.
│   │   ├── GameDebtHandler.cs
│   │   │   └── Human property selection and pending-debt settlement.
│   │   └── GameControlHandler.cs
│   │       └── Pause vote, resume, and surrender.
│   ├── GameLogic/
│   │   ├── GameEngine.cs
│   │   │   └── Core authoritative board, economy, debt, card, bankruptcy, and result rules.
│   │   ├── TurnTimer.cs
│   │   │   └── Turn/match countdown and automatic bot-turn trigger.
│   │   ├── DeckManager.cs
│   │   │   └── Creates and draws the 97-card global deck.
│   │   ├── Bots/
│   │   │   └── Bot AI action and decision logic.
│   │   └── RoomManager.cs
│   │       └── Placeholder; currently empty.
│   ├── Models/
│   │   ├── State/
│   │   │   └── Global server, room, game, player, and property state.
│   │   ├── ClientConnection.cs
│   │   │   └── TCP client plus authenticated/session metadata.
│   │   └── Events/
│   │       └── Card draw, ranking, leaderboard, and game-over payload models.
│   ├── Services/
│   │   ├── FirebaseApiService.cs
│   │   │   └── Firebase Auth and Realtime Database REST integration.
│   │   └── ServiceLocator.cs
│   │       └── Static service access.
│   └── Utils/PacketHelper.cs
│       └── Payload conversion compatibility helper.
├── Monopoly.Shared/
│   ├── Models/
│   │   ├── Packets/
│   │   │   └── Packet envelope, packet enum, and request/response DTOs.
│   │   ├── Configs/StaticData/
│   │   │   ├── BoardDatabase.cs
│   │   │   │   └── Canonical 32-square board configuration.
│   │   │   └── CardDatabase.cs
│   │   │       └── Canonical card IDs, tiers, descriptions, and effects.
│   │   └── Entities/
│   │       └── Shared/legacy user, room, property, and game entities.
│   └── Monopoly.Shared.csproj
│       └── .NET 8 shared class library.
├── Monopoly.Client.Unity/
│   ├── Assets/
│   │   ├── Scenes/
│   │   │   ├── SampleScene.unity
│   │   │   │   └── Splash, intro, login, and registration.
│   │   │   ├── LobbyScene.unity
│   │   │   │   └── Lobby menus and room workflow.
│   │   │   └── GameScene.unity
│   │   │       └── Board, 32 path points, dice, player panels, and action controls.
│   │   ├── Scripts/
│   │   │   ├── NetworkManager.cs
│   │   │   │   └── Persistent TCP client, packet processing, and UI event distribution.
│   │   │   ├── ServerConnectionConfig.cs
│   │   │   │   └── Local/LAN/public endpoint loading and command-line overrides.
│   │   │   ├── AuthManager.cs
│   │   │   │   └── Login/register UI and legacy raw auth-response reader.
│   │   │   ├── LobbyManager.cs
│   │   │   │   └── Lobby panels, room options, profile, resume, and session state.
│   │   │   ├── GameSession.cs
│   │   │   │   └── Static current room and game-state bridge.
│   │   │   ├── NetworkDataModels.cs
│   │   │   │   └── Unity-side DTOs matching server payloads.
│   │   │   ├── GameSceneUIBinder.cs
│   │   │   │   └── Binds scene controls and creates missing UI controllers.
│   │   │   ├── BoardTokenManager.cs
│   │   │   │   └── Board-point lookup, token creation, movement, and token effects.
│   │   │   ├── BoardTileInfoUI.cs
│   │   │   │   └── Full property/special-square popup and card-target interaction.
│   │   │   ├── DiceVisualUI.cs
│   │   │   │   └── Dice sprite loading and roll animation.
│   │   │   ├── PlayerInfoLayerUI.cs
│   │   │   │   └── Four-corner player summaries and turn/active labels.
│   │   │   ├── PropertyBuildMarkerUI.cs
│   │   │   │   └── Runtime house/hotel markers on property color bands.
│   │   │   ├── PlayerHandUI.cs
│   │   │   │   └── Held-card buttons and use-card requests.
│   │   │   ├── ChanceCardUI.cs
│   │   │   │   └── Drawn-card popup.
│   │   │   ├── GameChatUI.cs
│   │   │   │   └── Chat window, message input, history, and token bubbles.
│   │   │   ├── GameEventPopupUI.cs
│   │   │   │   └── Queued full-screen gameplay notifications.
│   │   │   ├── PropertySaleUI.cs
│   │   │   │   └── Selectable owned-property liquidation UI.
│   │   │   ├── GameSettingsUI.cs
│   │   │   │   └── Volume, mute, timer, pause vote, and surrender UI.
│   │   │   ├── AudioManager.cs
│   │   │   │   └── Persistent mixer/source volume and playback abstraction.
│   │   │   ├── GameOverUI.cs
│   │   │   │   └── Rankings, leaderboard request, and lobby return.
│   │   │   ├── RoomSlotUI.cs
│   │   │   │   └── Room-browser row rendering.
│   │   │   └── PlayerSlotUI.cs
│   │   │       └── Waiting-room slot rendering.
│   │   ├── Resources/
│   │   │   ├── DiceFaces/
│   │   │   │   └── Dice face sprites 1-6.
│   │   │   ├── TileImages/
│   │   │   │   └── City artwork loaded by normalized tile name.
│   │   │   └── UI/ChatPanel.prefab
│   │   │       └── Authored chat UI preferred over runtime fallback.
│   │   ├── Prefabs/
│   │   │   ├── Panel_GameScene.prefab
│   │   │   ├── PlayerSlot.prefab
│   │   │   └── RoomSlot.prefab
│   │   └── StreamingAssets/server-config.json
│   │       └── Deploy-time server host/port/timeout configuration.
│   ├── Packages/manifest.json
│   │   └── Unity package dependencies.
│   └── ProjectSettings/
│       └── Unity version, scene list, resolution, input, and player settings.
├── Monopoly.Client/
│   ├── Program.cs
│   ├── FormSplash.cs
│   └── FormLogin.cs
│       └── Incomplete Windows Forms network/authentication prototype.
├── MonopolyGame/Monopoly.Client.Unity/NT106.Q22.ANTT
│   └── Gitlink/nested legacy Unity snapshot; not part of the active root solution.
├── NT106_NEXT_FEATURES_AND_UI_GUIDE.md
├── NT106_FEATURE_STATUS_AND_TASK_PLAN.md
├── NT106_INTERNET_DEPLOYMENT_GUIDE.md
└── README.md / RUN.md
    └── Existing setup and development notes; some details predate current board/rules.
```

## 5. Conventions & Standards

### Naming

- C# types, methods, properties, and public members use `PascalCase`.
- Private/local variables generally use `camelCase`.
- Server namespaces follow `Monopoly.Server.*`.
- Shared namespaces follow `Monopoly.Shared.*`.
- Most Unity gameplay scripts currently use the global namespace.
- Packet type strings are inconsistent: some are uppercase snake case, while legacy messages use PascalCase.
- Unity scene objects use prefixes such as `Panel_`, `Btn_`, `Txt_`, and `Img_`.
- Board transforms must remain `BoardPoint_00` through `BoardPoint_31`.

### Patterns

- **Authoritative server:** The server computes and validates gameplay state.
- **Router/handler:** `PacketRouter` delegates packets to domain-specific static handlers.
- **Singleton:** `NetworkManager`, `AudioManager`, and `ServiceLocator`.
- **Observer/event distribution:** `NetworkManager` exposes events consumed by Unity UI modules.
- **Static repository/configuration:** `BoardDatabase`, `CardDatabase`, and `ServerState`.
- **DTO mirroring:** Unity maintains payload classes matching server public state.
- **Runtime UI fallback:** UI controllers create panels when an authored prefab or scene object is absent.
- **Lock ownership convention:** `Unsafe` helper names indicate the caller must hold `ServerState.Lock`.

### Git workflow

Repository history indicates:

- Feature branches such as `feature/logic_game`.
- Pull-request or merge commits into `main`.
- Free-form English and Vietnamese commit messages.
- No consistently enforced Conventional Commits format.
- Large feature commits sometimes mix server logic, Unity UI, assets, and configuration.

For future work, keep server protocol, shared DTOs, Unity DTO mirrors, and tests in the same focused change when they form one feature, while avoiding unrelated scene/assets churn.

### Shared rules and constants

Canonical static data:

- `Monopoly.Shared/Models/Configs/StaticData/BoardDatabase.cs`
- `Monopoly.Shared/Models/Configs/StaticData/CardDatabase.cs`

Important economy/rule values currently encoded in source:

| Rule | Current value |
|---|---|
| Starting money | 500,000 |
| Pass Start reward | 200,000 |
| Turn duration | 45 seconds |
| Match duration options | 10, 20, 30, 60 minutes |
| Tax charge | 100,000 |
| Lost Island final exit fee | 200,000 |
| Houses before hotel | 3 |
| First-round build cap | Max 1 house per city during the first full player round |
| First-place score | 100 |
| Second-place score | 50 |
| Third-place score | 20 |
| Remaining-place score | 5 |

Current board order:

```text
00 Bắt Đầu          08 World Tour       16 World Championship 24 Lost Island
01 Tokyo            09 New York         17 Berlin             25 Shanghai
02 Tax              10 Las Vegas        18 Cyprus             26 Beijing
03 Osaka            11 Chicago          19 Hamburg            27 Hong Kong
04 Chance           12 Chance           20 Chance             28 Bali
05 Paris            13 Sydney           21 Rome               29 Hà Nội
06 Hawaii           14 Dubai            22 Milan              30 Tax
07 Nice             15 London           23 Venice             31 Sài Gòn
```

Do not reorder board entries or scene board points independently. Token movement, tile click mapping, state positions, and visual placement all depend on the same index.

## 6. Known Issues & TODOs

### Critical security and protocol issues

1. **Sensitive packet logging:** Server receive/auth logs can include raw passwords, Firebase tokens, or complete authentication payloads.
2. **Hardcoded Firebase configuration:** API key and database endpoint are stored in source. Move them to environment or protected deployment configuration.
3. **Plain TCP:** No TLS, certificate validation, message signing, or encryption is implemented.
4. **Mixed auth protocol:** Authentication uses a raw pipe-delimited response and a separate stream reader, while the rest of the application uses JSON through `NetworkManager`.
5. **Weak resume identity:** Resume searches by username instead of a validated UID/session token.
6. **Profile update trust:** Some update data is accepted from payload fields instead of being bound strictly to the authenticated connection.
7. **No input throttling:** There is no packet rate limit, maximum message size, or abuse protection.

### Gameplay and state issues

1. A disconnect in a two-human game can immediately satisfy the game-over condition, preventing meaningful resume.
2. Rooms and matches exist only in memory; server restart loses all state.
3. Finished rooms can remain in server dictionaries longer than necessary.
4. The card deck is global across every room, so concurrent rooms draw from the same 97-card deck.
5. Building does not require a complete color set.
6. ~~Human double/triple-double behavior is less complete than bot behavior.~~ **Resolved (2026-06-14):** both humans and bots now re-roll on doubles and go to Lost Island on the third consecutive double.
7. Pause requires voting, but any active player can resume without a vote.
8. ~~Some non-timeout game-over paths do not provide a detailed `EndReason`.~~ **Resolved (2026-06-14):** last-player-standing and disconnect finishes now set `EndReason`, and the reason is sent in `GAME_OVER` and displayed.
9. Bot AI includes duplicate or older debt/sale decisions separate from the central engine.
10. ~~A forced-double message contains a likely non-interpolated `${player.Username}` string.~~ **Fixed (2026-06-14):** now a proper C# interpolated string in `GameHandler.cs`.
11. Some asynchronous bot tasks can continue briefly after client exit or room-state changes.
12. Match result persistence depends on the connection still holding Firebase identity information.
13. There is no server-state persistence, replay log, or crash recovery.

### Firebase and persistence issues

1. Match score updates are non-transactional REST read-modify-write operations.
2. No retry queue exists for failed score, win/loss, or match-history writes.
3. Firebase errors can be reduced to generic user messages.
4. Gameplay money is server memory; the Firebase profile `Money` field is not the live match balance.
5. No password-reset flow is implemented.
6. No explicit Firebase security-rule documentation or emulator configuration was found.

### Unity and UI issues

1. Vietnamese source/UI text contains mojibake in several files.
2. TextMesh Pro reports missing Vietnamese glyphs in `LiberationSans SDF` and fallback assets.
3. Several large UI surfaces are generated at runtime, making visual authoring and responsive layout harder.
4. `BoardTileInfoUI.cs` is a large multi-responsibility script.
5. Scene and runtime tile-popup objects can coexist, creating duplicate/legacy hierarchy state.
6. Tile artwork is loaded by normalized resource name; missing or differently named files leave the image area blank.
7. Current tracked tile images do not cover all resort squares.
8. `TileImages/LYON` remains although Lyon is no longer in the current board database.
9. Property build markers show the actual house count as 1–3 discrete house icons in a centered row (procedurally drawn body + roof, tinted by owner color); a hotel replaces them with a single wide red building (procedural trapezoid-roof sprite). `Resources/UI/house`/`hotel` sprites are used if supplied, else the code-drawn sprites are used (no asset required).
10. No final `Resources/UI/GameSettingsPanel.prefab` was found, so settings commonly use runtime fallback.
11. Fixed pixel positions and sizes can overlap at resolutions other than the primary 1920x1080 target.
12. Audio event hookups now exist (dice/buy/build/card/game-over/UI-click/token-jump SFX + GameScene background music), loading clips by name from `Resources/Audio/`; `build`, `click`, `dice`, and `jump` assets exist, while `buy`, `card`, `gameover`, and `bgm` still need to be added.
13. Chat/settings launchers are shifted left to avoid the four-player bottom-right player panel, but the layout is still fixed-pixel and should be checked after resolution changes.
14. Some UI scripts search objects by name, creating fragile scene coupling.
15. Direct `StreamingAssets` file access is suitable for the current desktop target but is not portable to every Unity platform.

### Networking and concurrency issues

1. Network writes do not have a clear per-connection send queue/lock.
2. Broadcast failures are frequently swallowed.
3. There is no heartbeat to distinguish a slow client from a dead connection.
4. There is no graceful cancellation/shutdown path for all loops and delayed bot tasks.
5. Public state is rebroadcast as a full snapshot rather than versioned deltas.
6. Packet types are duplicated between string literals, an incomplete enum, and Unity handlers.
7. Shared protocol DTOs and Unity mirror DTOs can drift.
8. There is no automated protocol compatibility test.

### Code quality and testing

1. `RoomManager.cs` is empty.
2. The shared `PacketType` enum is incomplete/stale and includes unimplemented values.
3. The legacy WinForms client is not a complete playable client.
4. Existing markdown documentation contains older board names and mappings.
5. No source unit, integration, network-framing, gameplay-rule, or Unity PlayMode/EditMode tests were found.
6. `dotnet build MonopolyGame.sln` succeeds but currently reports approximately 112 warnings, primarily nullable-reference warnings plus duplicate/unused code warnings.
7. Unity compilation and scene behavior require Unity Editor verification; they are not covered by the .NET solution build.
8. Large static classes and global state make isolated tests difficult.
9. Packet handling, rule calculation, persistence, and presentation are sometimes combined in the same methods.
10. No CI workflow was found to build the .NET solution or validate Unity assets/scenes.

## 7. Dependencies & External Services

### Server-side NuGet and framework dependencies

| Project | Dependency | Version/purpose |
|---|---|---|
| `Monopoly.Server` | .NET | `net8.0` |
| `Monopoly.Server` | Newtonsoft.Json | 13.0.4, JSON packets and Firebase responses |
| `Monopoly.Server` | `Monopoly.Shared` | Project reference |
| `Monopoly.Shared` | .NET | `net8.0` |
| `Monopoly.Client` | .NET Windows | `net8.0-windows`, WinForms |
| `Monopoly.Client` | Newtonsoft.Json | 13.0.4 |
| `Monopoly.Client` | `Monopoly.Shared` | Project reference |

No database driver or web framework is used by the server; TCP and HTTP calls use the .NET base class libraries.

### Unity packages and assets

Key package manifest entries:

- `com.unity.nuget.newtonsoft-json` 3.2.2
- `com.unity.textmeshpro` 3.0.7
- `com.unity.ugui` 1.0.0 through the Unity package set
- Unity built-in audio, UI, image conversion, physics, input, and networking modules

Tracked asset groups include:

- TextMesh Pro resources.
- Liberation Sans and Arial-derived font assets.
- Board backgrounds and board-square artwork.
- Dice face sprites.
- City tile images.
- House/hotel or board sprites under Unity asset folders.
- Chat, room slot, player slot, and game panel prefabs.

Third-party asset licensing is not documented comprehensively in the repository and should be verified before external distribution.

### Firebase configuration

The server uses:

- Firebase Identity Toolkit REST API for login and registration.
- Firebase Realtime Database REST API for user profiles and leaderboard data.

Project-specific API keys, database URLs, tokens, and user credentials are intentionally not reproduced in this summary. Current source configuration should be replaced by values such as:

```text
FIREBASE_API_KEY
FIREBASE_DATABASE_URL
```

Recommended production safeguards:

- Store secrets/configuration outside Git.
- Restrict Firebase API keys by service and application where applicable.
- Define and audit Realtime Database security rules.
- Avoid logging ID tokens, passwords, or raw auth packets.
- Use HTTPS for Firebase calls and TLS for the game transport.

### Runtime and deployment prerequisites

- .NET 8 SDK/runtime for the server.
- Unity 2022.3.62f3 for editing and building the game.
- Windows desktop is the currently configured client target.
- TCP port `8080` by default, or a configured alternative.
- LAN firewall permission for local-network play.
- Public server/VPS or router port forwarding for Internet play.
- A client build configured with the reachable server hostname/IP.
- Firebase project connectivity for authentication and leaderboard features.

### Verification snapshot

- `dotnet build MonopolyGame.sln`: **successful**, 0 errors.
- Current build warnings: approximately **114**.
- Unity scenes and prefabs: inspected statically; no automated Unity build or PlayMode test suite was found.

### Latest repository sync

- Latest pushed branch: `feature/logic_game`.
- Latest pushed commit: `0b828b6 add gameplay updates and project summary`.
- Commit timestamp: June 7, 2026 23:19:05 +0700.
- Remote target: `origin/feature/logic_game`.
- This summary reflects the committed gameplay/UI/server updates, the added `GameEngineTests.cs`, and the project documentation snapshot included in that push.

## 8. Change Log

### 2026-06-14 — Money-flow corner boxes + house-block build markers (client)

- **Per-player money boxes + payment animation (new `MoneyFlowUI.cs`):** A new runtime UI (`EnsureExists`, bootstrapped in `GameSceneUIBinder` and refreshed from `NetworkManager`'s `GAME_STATE_UPDATE`) draws a money box at each of the 4 corners showing each player's cash (formatted `366 000`). On every state update it diffs each player's `Money` against the previous snapshot and flies a code-drawn money chip from the payer's box **into the board center, then out to the receiver's box** (board center is a hub, so player↔player and player↔bank both work). First state / resume only seeds the snapshot (no spurious chips). Chip/box sprites are generated in code; animation reuses the `SmoothStep + Lerp + Sin`-arc coroutine pattern. `BoardTokenManager` gained `TryGetBoardCenterWorldPosition()` for the center point.
- **Count-based build markers (`PropertyBuildMarkerUI.cs`):** Replaced the previous 3 colored bars with **1–3 discrete house icons** (procedurally drawn body + triangular roof via `CreateHouseSprite`, tinted by owner color) laid out in a centered horizontal row matching the actual house count; a **hotel** replaces the cluster with **one wide red building** (procedural trapezoid-roof `CreateHotelSprite`). Still prefers `Resources/UI/house`/`hotel` sprites when present, otherwise uses the code-drawn sprites (no asset needed). *(Note: an interim "single growing block" version was iterated to this count-based layout per design feedback.)*

**Verification:** Unity recompiled with 0 errors (checked via MCP `read_console`). Visual behavior (corner box placement, fly animation, house block sizing) requires in-Editor / in-game verification.

### 2026-06-14 — Core rules + audio/visual pass

**Theme A — Core gameplay rules (server):**

- **Doubles & triple-double jail (A1):** Rolling doubles now grants an extra roll for human players (mirroring existing bot behavior), and three consecutive doubles sends the player straight to Lost Island without applying the landed square. Tracked via the existing `GamePlayerState.ConsecutiveDoubles`, reset on turn change. Implemented in `Handles/GameHandler.cs` (`HandleDiceRollAsync`), `GameLogic/Bots/BotAIController.cs`, and `GameLogic/GameEngine.cs` (`StartNextTurnUnsafe` reset). The non-interpolated `${player.Username}` forced-double log string was fixed here too. Client needed no change — `rollButton.interactable = isMyTurn && !HasRolledThisTurn` already re-enables the roll and `End Turn` stays gated on `HasRolledThisTurn`.
- **Complete `EndReason` (A2):** `EndReason` is now set for last-player-standing and "all players left/bankrupt" (`GameEngine.ResolveBankruptcyAndWinnerUnsafe`) and opponent-disconnect (`GameHandler` leave/disconnect). The `GAME_OVER` packet now carries a `Reason` field (`NetworkSender.BroadcastGameOverAsync`, defaulting to "Trận đấu kết thúc"); the client `GameOverData` gained `Reason` and `GameOverUI` displays it.

**Theme B — Audio & visuals (client):**

- **Audio event wiring (B1):** `AudioManager` gained `PlaySfx(string)` / `PlayMusic(string)` overloads that load by name from `Resources/Audio/` (cached, null-safe). SFX fire on dice roll (`DiceVisualUI`), buy/build (`NetworkManager`), card draw and game over (`NetworkManager`), plus background music on entering GameScene (`GameSceneUIBinder`). Missing clip files simply no-op.
- **Build marker sprites (B2):** `PropertyBuildMarkerUI` loads `Resources/UI/house` and `Resources/UI/hotel` sprites when present (lightly tinted by owner color) and falls back to the previous colored bars when absent.

**Assets still to add (code degrades gracefully without them):** `Assets/Resources/Audio/{dice,card,gameover,buy,build,bgm}` and `Assets/Resources/UI/{house,hotel}.png`.

**Out of scope this pass (deliberately not changed):** building does not require owning the complete color group; bot held-card usage; per-room deck; finished-room cleanup; remaining mojibake log strings.

**Verification:** `dotnet build MonopolyGame.sln` succeeds with 0 errors. `dotnet run --project Monopoly.Server -- --run-tests` passes all `GameEngineTests`, including the new `TestEndReasonLastPlayerStanding` and `TestSendPlayerToIsland`. Client (Unity) changes require Editor verification and are not covered by the .NET build. A1's per-turn doubles flow lives in the packet handler and is verified manually rather than by `GameEngineTests`.
