# 🎲 Kế Hoạch Nâng Cấp Logic Game Monopoly (v2)

> **Mục tiêu:** Tích hợp các ô đặc biệt (Cơ Hội, Du Lịch Thế Giới, Giải Vô Địch, Đảo Hoang) và hệ thống thẻ bài hoàn chỉnh vào server/client hiện tại.
>
> **Phiên bản v2 — Cập nhật theo review:**
> - Packet theo format JSON `{ Type, Payload }` — không dùng pipe-separated string
> - UI thẻ tách riêng thành `ChanceCardUI.cs` và `PlayerHandUI.cs`, không nhét vào `BoardTileInfoUI.cs`
> - Trạng thái bỏ lượt tách rõ: `IsOnIsland` + `SkipTurnsLeft` + `SkipReason` thay vì dùng chung `JailTurnsLeft`
> - Không dùng packet `MOVE_TOKEN` riêng — vẫn sync hoàn toàn qua `GAME_STATE_UPDATE` + các field `LastMove*`

---

## 📋 Thứ Tự Implement Đề Xuất

```
Bước 1 → RoomModels.cs + NetworkDataModels.cs   (thêm fields, không có logic)
Bước 2 → Program.cs: Chance cơ bản              (FINE, JACKPOT, GO_TO_JAIL, SKIP_TURN, TAX_PENALTY)
Bước 3 → Program.cs: Island logic               (vào / ở lại / thoát đảo)
Bước 4 → Program.cs: WorldTour + Championship
Bước 5 → NetworkManager.cs                      (handler cho các Type packet mới)
Bước 6 → ChanceCardUI.cs + PlayerHandUI.cs      (tạo mới, hiển thị thẻ + tay bài)
Bước 7 → Program.cs: thẻ cần chọn target        (EARTHQUAKE, POWER_OUTAGE, MOVE_CHAMPIONSHIP)
Bước 8 → Program.cs: thẻ chủ động              (FLIGHT, FORCE_DOUBLE, ESCAPE_ISLAND, FREE_UPGRADE, FREE_RENT)
```

---

## 1. `RoomModels.cs` — Server

### Thêm vào `GamePlayerState`

```csharp
// Thẻ đang giữ trong tay
public bool HasFreeRentCard { get; set; }       // FREE_RENT
public bool HasEscapeIslandCard { get; set; }   // ESCAPE_ISLAND
public bool HasFlightCard { get; set; }         // FLIGHT
public bool HasFreeUpgradeCard { get; set; }    // FREE_UPGRADE
public bool HasForceDoubleCard { get; set; }    // FORCE_DOUBLE

// Trạng thái bỏ lượt — tách rõ thay vì dùng chung JailTurnsLeft
public bool IsOnIsland { get; set; }            // đang bị giam ở Đảo Hoang (logic đặc biệt: lắc đôi thoát, hết 3 lượt trả 200K)
public int SkipTurnsLeft { get; set; }          // số lượt còn phải bỏ (dùng cho WorldTour, SKIP_TURN card, v.v.)
public string SkipReason { get; set; }          // "WORLD_TOUR" | "CARD_SKIP" — để client hiển thị lý do đúng

// Thông tin thẻ vừa rút (để client animate)
public string LastDrawnCardId { get; set; }
```

> **Giữ nguyên `JailTurnsLeft`** trong `GamePlayerState` nếu đang được dùng ở logic khác — nhưng từ nay chỉ dùng cho Đảo Hoang (đếm số lượt còn lại trên đảo, max 3). `SkipTurnsLeft` dùng cho mọi trường hợp bỏ lượt không phải đảo.

### Thêm vào `GameState`

```csharp
public int WorldChampionshipPosition { get; set; } = 16;  // vị trí hiện tại của ô Championship (mặc định ô 16)
public bool IsWaitingForCardChoice { get; set; }           // server đang chờ client chọn target trước khi tiếp tục
public string PendingCardEffectCode { get; set; }          // effect đang chờ: "EARTHQUAKE" | "POWER_OUTAGE" | "MOVE_CHAMPIONSHIP" | "FLIGHT" | "FREE_UPGRADE"
public string PendingCardPlayerUsername { get; set; }      // username của người phải chọn
```

---

## 2. `NetworkDataModels.cs` — Client Unity

Mirror **đúng y hệt** các field đã thêm ở `RoomModels.cs` vào hai class tương ứng:

### Thêm vào `GamePlayerStateData`

```csharp
public bool HasFreeRentCard;
public bool HasEscapeIslandCard;
public bool HasFlightCard;
public bool HasFreeUpgradeCard;
public bool HasForceDoubleCard;
public bool IsOnIsland;
public int SkipTurnsLeft;
public string SkipReason;
public string LastDrawnCardId;
```

### Thêm vào `GameStateData`

```csharp
public int WorldChampionshipPosition;
public bool IsWaitingForCardChoice;
public string PendingCardEffectCode;
public string PendingCardPlayerUsername;
```

---

## 3. `Program.cs` — Server (Phần lớn nhất)

### Convention packet JSON của project

Toàn bộ packet giữa server và client theo format:
```json
{ "Type": "TEN_PACKET", "Payload": { ... } }
```

Server serialize rồi gửi qua `NetworkStream`. Client deserialize và switch theo `Type`.  
**Không dùng pipe-separated string** (`USE_CARD|FLIGHT`) — đây là convention cũ, không nhất quán với phần còn lại.

---

### 3A. Xử lý ô Cơ Hội — `HandleChanceSquare(player, gameState)`

```
1. Random rút 1 thẻ từ CardDatabase.Cards
2. Ghi player.LastDrawnCardId = cardId
3. Broadcast packet CARD_DRAWN cho tất cả client:
   {
     "Type": "CARD_DRAWN",
     "Payload": {
       "DrawnByUsername": "<username>",
       "CardId": "<cardId>",
       "CardName": "<name>",
       "CardType": "<Golden|Silver|Wooden>",
       "DetailEffect": "<mô tả>"
     }
   }

4. Phân nhánh theo EffectCode:

   FINE           → trừ 100,000 tiền mặt của player, rồi BroadcastGameStateUpdate
   JACKPOT        → cộng 500,000 từ bank vào player, rồi BroadcastGameStateUpdate
   GO_TO_JAIL     → gọi HandleSendToIsland(player, gameState)
   SKIP_TURN      → player.SkipTurnsLeft = 1, player.SkipReason = "CARD_SKIP"
   TAX_PENALTY    → trừ 10% tổng tiền mặt (làm tròn xuống bội số 1000)
   CHARITY_PAY    → trừ 50,000 của player, cộng 50,000 cho từng player còn lại không phá sản (xem mục CHARITY_PAY)
   GO_TO_WORLD_TOUR → di chuyển player đến ô 8 (cập nhật LastMoveToPosition, LastFinalPosition),
                      player.SkipTurnsLeft = 1, player.SkipReason = "WORLD_TOUR"

   FREE_RENT      → player.HasFreeRentCard = true
   FLIGHT         → player.HasFlightCard = true
   ESCAPE_ISLAND  → player.HasEscapeIslandCard = true
   FREE_UPGRADE   → player.HasFreeUpgradeCard = true
   FORCE_DOUBLE   → player.HasForceDoubleCard = true

   EARTHQUAKE     → gameState.IsWaitingForCardChoice = true
                    gameState.PendingCardEffectCode = "EARTHQUAKE"
                    gameState.PendingCardPlayerUsername = player.Username
                    Gửi riêng cho player packet REQUEST_CARD_CHOICE (xem mục 3E)

   POWER_OUTAGE   → gameState.IsWaitingForCardChoice = true
                    gameState.PendingCardEffectCode = "POWER_OUTAGE"
                    gameState.PendingCardPlayerUsername = player.Username
                    Gửi riêng cho player packet REQUEST_CARD_CHOICE

   MOVE_CHAMPIONSHIP → gameState.IsWaitingForCardChoice = true
                       gameState.PendingCardEffectCode = "MOVE_CHAMPIONSHIP"
                       gameState.PendingCardPlayerUsername = player.Username
                       Gửi riêng cho player packet REQUEST_CARD_CHOICE

5. Sau tất cả: BroadcastGameStateUpdate(gameState)
```

---

### 3B. Xử lý Đảo Hoang — `HandleIslandLogic`

#### Khi bị đày vào đảo — `HandleSendToIsland(player, gameState)`

```
- player.IsOnIsland = true
- player.JailTurnsLeft = 3
- player.Position = 24
- Cập nhật gameState: LastMoveToPosition = 24, LastFinalPosition = 24
- Thêm vào ActionLog: "{Username} bị đày ra Đảo Hoang!"
- BroadcastGameStateUpdate(gameState)   ← client đọc LastFinalPosition để animate token
```

#### Đầu lượt của player đang ở đảo — `HandleIslandTurn(player, dice1, dice2, gameState)`

Kiểm tra theo thứ tự ưu tiên:

```
1. Nếu player vừa gửi packet USE_CARD với CardId = ESCAPE_ISLAND (xử lý trước khi roll):
   → player.HasEscapeIslandCard = false
   → player.IsOnIsland = false, player.JailTurnsLeft = 0
   → Cho phép di chuyển bình thường từ ô 24 với tổng dice
   → ActionLog: "{Username} dùng Trực Thăng Cứu Hộ — thoát Đảo Hoang!"

2. Nếu dice1 == dice2 (lắc ra đôi):
   → player.IsOnIsland = false, player.JailTurnsLeft = 0
   → Di chuyển bình thường với tổng dice
   → KHÔNG áp dụng luật "lắc đôi đi thêm lượt" trong trường hợp này
   → ActionLog: "{Username} lắc đôi — thoát Đảo Hoang miễn phí!"

3. Nếu player.JailTurnsLeft > 1:
   → player.JailTurnsLeft--
   → Không di chuyển, kết thúc lượt
   → ActionLog: "{Username} còn {JailTurnsLeft} lượt trên Đảo Hoang"

4. Nếu player.JailTurnsLeft == 1 (hết hạn):
   → player.JailTurnsLeft = 0, player.IsOnIsland = false
   → Trừ 200,000 tiền mặt (phí chuộc)
   → Di chuyển theo dice từ ô 24
   → ActionLog: "{Username} trả 200,000 để rời Đảo Hoang"
```

---

### 3C. Xử lý ô Du Lịch Thế Giới (ô 8) — `HandleWorldTourSquare(player, gameState)`

```
- player.SkipTurnsLeft = 1
- player.SkipReason = "WORLD_TOUR"
- ActionLog: "{Username} đến Du Lịch Thế Giới — chờ cất cánh lượt sau"
- BroadcastGameStateUpdate(gameState)

Đầu lượt kế tiếp (kiểm tra trước khi cho lắc xúc xắc):
  if (player.SkipTurnsLeft > 0 && !player.IsOnIsland)
  {
      player.SkipTurnsLeft--;
      ActionLog: reason == "WORLD_TOUR" ? "{Username} đang chờ cất cánh — bỏ lượt" : "{Username} bị đóng băng giao dịch — bỏ lượt"
      BroadcastGameStateUpdate(gameState)
      AdvanceToNextTurn(gameState)
      return;
  }
```

> **Tại sao tách `SkipTurnsLeft` khỏi `JailTurnsLeft`:**
> `JailTurnsLeft` chứa logic phức tạp (lắc đôi thoát, trả tiền chuộc, tối đa 3 lượt). `SkipTurnsLeft` chỉ đơn giản là "bỏ N lượt rồi thôi". Dùng chung dễ sinh bug khi player vừa ở WorldTour vừa có SKIP_TURN card.

---

### 3D. Xử lý ô Giải Vô Địch — `HandleChampionshipSquare(player, gameState)`

```
- Xác định vị trí Championship từ gameState.WorldChampionshipPosition
- Nếu player.Position != gameState.WorldChampionshipPosition → bỏ qua (ô bình thường)
- Thu tiền từ tất cả player không phá sản, không phải chính player:
    foreach other: other.Money -= 100000 (hoặc tất cả tiền nếu không đủ → phá sản)
    player.Money += tổng thu được
- ActionLog: "🏆 Giải Vô Địch! {Username} thu được {total} từ các đối thủ!"
- BroadcastGameStateUpdate(gameState)
```

---

### 3E. Packet `REQUEST_CARD_CHOICE` — Server gửi riêng cho 1 client

Khi thẻ cần chọn target, server gửi riêng cho client đang dùng thẻ:

```json
{
  "Type": "REQUEST_CARD_CHOICE",
  "Payload": {
    "EffectCode": "EARTHQUAKE",
    "ChoiceLabel": "Chọn thành phố của đối thủ để phá hủy 1 cấp",
    "ValidPositions": [9, 10, 11, 13, 15]
  }
}
```

`ValidPositions` là danh sách positionIndex hợp lệ để chọn (server tính sẵn, client chỉ highlight đúng ô đó).

---

### 3F. Packet `CARD_CHOICE_MADE` — Client gửi lên Server

```json
{
  "Type": "CARD_CHOICE_MADE",
  "Payload": {
    "EffectCode": "EARTHQUAKE",
    "ChosenPosition": 10
  }
}
```

Server validate:
- `gameState.IsWaitingForCardChoice == true`
- `gameState.PendingCardPlayerUsername == sender.Username`
- `ChosenPosition` nằm trong danh sách hợp lệ

Xử lý theo `EffectCode`:

```
EARTHQUAKE:
  → property = gameState.Properties[ChosenPosition]
  → Nếu HasHotel: HasHotel = false, HouseCount = 3
  → Nếu HouseCount > 0: HouseCount--
  → ActionLog: "Động đất! {thành phố} của {owner} bị phá hủy 1 cấp"

POWER_OUTAGE:
  → property.PowerOutageTurn = gameState.TurnNumber + 2
  → ActionLog: "Cúp điện! {thành phố} của {owner} mất hiệu lực 2 lượt"

MOVE_CHAMPIONSHIP:
  → gameState.WorldChampionshipPosition = ChosenPosition
  → ActionLog: "{Username} dời Giải Vô Địch về {thành phố}"

FLIGHT:
  → Tính đi ngang Bắt Đầu: nếu ChosenPosition < player.Position → player.Money += 300000
  → player.Position = ChosenPosition
  → Cập nhật LastMoveFromPosition, LastMoveToPosition, LastFinalPosition
  → Xử lý ô tại ChosenPosition như bình thường

FREE_UPGRADE:
  → property = gameState.Properties[ChosenPosition]
  → Nếu HouseCount < 3: HouseCount++
  → Nếu HouseCount == 3: HasHotel = true, HouseCount = 0
  → ActionLog: "{Username} nâng cấp miễn phí {thành phố}"

→ Sau mỗi case: gameState.IsWaitingForCardChoice = false, PendingCardEffectCode = ""
→ BroadcastGameStateUpdate(gameState)
```

---

### 3G. Packet `USE_CARD` — Client gửi lên Server (thẻ chủ động)

```json
{
  "Type": "USE_CARD",
  "Payload": {
    "CardEffectCode": "FORCE_DOUBLE"
  }
}
```

Server validate rồi xử lý theo `CardEffectCode`:

```
FORCE_DOUBLE:
  → Chỉ hợp lệ khi đến lượt player và HasRolledThisTurn == false
  → player.HasForceDoubleCard = false
  → Set flag tạm ForceDoubleThisTurn = true trong GameState (thêm field bool)
  → Khi xử lý roll: nếu ForceDoubleThisTurn == true thì random dice1 (1-6), dice2 = dice1
  → ForceDoubleThisTurn = false sau khi đã áp dụng

ESCAPE_ISLAND:
  → Chỉ hợp lệ khi player.IsOnIsland == true
  → Xử lý như bước 1 trong HandleIslandTurn (xem 3B)

FLIGHT:
  → Chỉ hợp lệ khi đến lượt player
  → player.HasFlightCard = false
  → Tính ValidPositions = tất cả 32 ô trừ ô hiện tại
  → Set IsWaitingForCardChoice = true, PendingCardEffectCode = "FLIGHT"
  → Gửi REQUEST_CARD_CHOICE cho client

FREE_UPGRADE:
  → Chỉ hợp lệ khi đến lượt player
  → player.HasFreeUpgradeCard = false
  → Tính ValidPositions = thành phố thuộc sở hữu player chưa đạt Hotel
  → Set IsWaitingForCardChoice = true, PendingCardEffectCode = "FREE_UPGRADE"
  → Gửi REQUEST_CARD_CHOICE cho client
```

> **Lưu ý:** `FREE_RENT` không có packet USE_CARD — server tự kiểm tra và tiêu thẻ trong `HandleRentPayment`.

---

### 3H. Kiểm tra `FREE_RENT` trong `HandleRentPayment`

```csharp
// Gọi TRƯỚC khi trừ tiền phạt
if (player.HasFreeRentCard)
{
    player.HasFreeRentCard = false;
    gameState.ActionLog.Add($"{player.Username} dùng Khiên Miễn Trừ — không phải trả phạt!");
    BroadcastGameStateUpdate(gameState);
    return;
}
```

---

### 3I. Kiểm tra `POWER_OUTAGE` trong `HandleRentPayment`

```csharp
// Gọi TRƯỚC khi tính tiền phạt ô
if (property.PowerOutageTurn >= gameState.TurnNumber)
{
    gameState.ActionLog.Add($"{property.Name} đang mất điện — miễn phạt lượt này!");
    BroadcastGameStateUpdate(gameState);
    return;
}
```

---

## 4. `NetworkManager.cs` — Client Unity

### Packet Type mới nhận từ Server

| Type | Payload chính | Xử lý phía client |
|---|---|---|
| `CARD_DRAWN` | `DrawnByUsername`, `CardId`, `CardName`, `CardType`, `DetailEffect` | Gọi `ChanceCardUI.ShowCard(...)` để hiển thị popup |
| `REQUEST_CARD_CHOICE` | `EffectCode`, `ChoiceLabel`, `ValidPositions[]` | Highlight các ô hợp lệ trên board, chờ player chọn |
| `GAME_STATE_UPDATE` | toàn bộ `GameStateData` | Như hiện tại — bổ sung đọc thêm các field mới (`IsOnIsland`, `SkipTurnsLeft`, `WorldChampionshipPosition`, v.v.) |

> **Không cần packet riêng** như `CHAMPIONSHIP_MOVED` hay `WORLD_TOUR_TRIGGERED` — tất cả đều phản ánh qua `GAME_STATE_UPDATE`. Client đọc `WorldChampionshipPosition` để vẽ icon Championship đúng ô, đọc `SkipReason` để hiển thị thông báo phù hợp.

### Packet Type mới gửi từ Client lên Server

| Type | Payload | Điều kiện |
|---|---|---|
| `USE_CARD` | `{ "CardEffectCode": "FORCE_DOUBLE" }` | Đến lượt, chưa lắc (hoặc đang ở đảo với ESCAPE_ISLAND) |
| `CARD_CHOICE_MADE` | `{ "EffectCode": "FLIGHT", "ChosenPosition": 15 }` | Khi server đang chờ (`IsWaitingForCardChoice == true`) |

---

## 5. File UI mới — Client Unity

### `ChanceCardUI.cs` (tạo mới)

Chịu trách nhiệm **hiển thị thẻ vừa rút**. Không liên quan đến `BoardTileInfoUI.cs`.

```
- static EnsureExists() / singleton pattern như các UI khác trong project
- Nhận sự kiện từ NetworkManager khi parse được packet CARD_DRAWN
- ShowCard(cardId, cardName, cardType, detailEffect):
    + Hiển thị popup với tên thẻ và mô tả chi tiết
    + Màu viền theo loại: Gold (#FFD700), Silver (#C0C0C0), Wooden (#8B5E3C)
    + Animation: scale-in + fade-in
    + Tự đóng sau 3 giây HOẶC có nút "Đã hiểu"
    + Nếu thẻ thuộc loại "giữ lại" (HasXxxCard): thêm text nhỏ "Thẻ được giữ trong tay"
```

### `PlayerHandUI.cs` (tạo mới)

Chịu trách nhiệm **hiển thị tay bài của local player** và cho phép dùng thẻ.

```
- Cập nhật mỗi khi nhận GAME_STATE_UPDATE
- Tìm GamePlayerStateData của local player (so sánh Username với PlayerPrefs hoặc session)
- Hiển thị danh sách thẻ đang giữ, mỗi thẻ là 1 UI element gồm: tên thẻ + nút "Dùng"

Điều kiện enable nút "Dùng" theo từng thẻ:
  FORCE_DOUBLE   → đến lượt mình VÀ HasRolledThisTurn == false
  ESCAPE_ISLAND  → IsOnIsland == true (bất kể lượt ai)
  FLIGHT         → đến lượt mình
  FREE_UPGRADE   → đến lượt mình VÀ còn thành phố chưa đạt Hotel
  FREE_RENT      → không có nút — hiển thị nhãn "Tự động kích hoạt"

Khi nhấn "Dùng":
  → Gửi packet USE_CARD lên server:
    { "Type": "USE_CARD", "Payload": { "CardEffectCode": "<effectCode>" } }

Khi server gửi REQUEST_CARD_CHOICE:
  → PlayerHandUI ẩn đi, BoardTileInfoUI (hoặc một overlay riêng) highlight các ô ValidPositions
  → Khi player chọn ô → gửi CARD_CHOICE_MADE
```

### `BoardTileInfoUI.cs` (chỉnh sửa nhỏ)

Không thêm logic thẻ vào đây. Chỉ bổ sung:

```
- Hiển thị icon ⚡ trên tile đang bị PowerOutage (đọc từ GamePropertyStateData.PowerOutageTurn)
- Hiển thị icon 🏆 tại ô có positionIndex == GameStateData.WorldChampionshipPosition
- Hỗ trợ highlight ô khi nhận REQUEST_CARD_CHOICE (gọi từ NetworkManager)
```

---

## ⚠️ Lưu Ý Quan Trọng

### Tổng hợp trạng thái bỏ lượt

| Trường hợp | `IsOnIsland` | `JailTurnsLeft` | `SkipTurnsLeft` | `SkipReason` | Hành vi |
|---|---|---|---|---|---|
| Đảo Hoang | `true` | 1–3 | 0 | — | Lắc đôi thoát / hết lượt trả 200K |
| Du Lịch Thế Giới | `false` | 0 | 1 | `"WORLD_TOUR"` | Bỏ đúng 1 lượt |
| Thẻ SKIP_TURN | `false` | 0 | 1 | `"CARD_SKIP"` | Bỏ đúng 1 lượt |
| Bình thường | `false` | 0 | 0 | — | Di chuyển bình thường |

### `WorldChampionshipPosition`

- Mặc định = `16` theo `BoardDatabase`
- Thẻ `MOVE_CHAMPIONSHIP` thay đổi giá trị này trong `GameState`
- **Không hardcode** — server lưu trong `GameState`, sync xuống client qua `GAME_STATE_UPDATE`
- Client đọc field này để vẽ icon 🏆 đúng vị trí

### `PowerOutageTurn`

- Đã có sẵn trong `GamePropertyState` — tận dụng field này
- Logic: `if (property.PowerOutageTurn >= gameState.TurnNumber)` thì miễn phạt
- Không cần server chủ động giảm counter — so sánh trực tiếp với `TurnNumber` là đủ

### `CHARITY_PAY` — Vòng lặp đúng cách

```csharp
long totalPaid = 0;
foreach (var other in gameState.Players)
{
    if (other.Username == player.Username) continue;
    if (other.IsBankrupt) continue;

    long toPay = Math.Min(50000, player.Money);  // không trả hơn số tiền đang có
    player.Money -= toPay;
    other.Money += toPay;
    totalPaid += toPay;

    if (player.Money <= 0)
    {
        player.IsBankrupt = true;
        break;  // hết tiền thì dừng
    }
}
gameState.ActionLog.Add($"{player.Username} từ thiện {totalPaid} cho các đối thủ");
```

### Validation phía Server cho mọi packet mới

Server phải check trước khi xử lý:

```
USE_CARD:
  - Đúng lượt của người gửi (CurrentTurnUsername == sender.Username)
  - Player thực sự đang giữ thẻ đó (HasXxxCard == true)
  - Điều kiện dùng thẻ thỏa mãn (xem bảng ở PlayerHandUI)

CARD_CHOICE_MADE:
  - gameState.IsWaitingForCardChoice == true
  - gameState.PendingCardPlayerUsername == sender.Username
  - ChosenPosition nằm trong ValidPositions đã tính lúc gửi REQUEST_CARD_CHOICE
```

---

*Tài liệu dựa trên codebase: `RoomModels.cs`, `NetworkDataModels.cs`, `NetworkManager.cs`, `Program.cs`, `BoardTileInfoUI.cs`, `Constant.cs`.*
*Cập nhật v2: packet JSON, UI tách file, trạng thái bỏ lượt rõ ràng, sync qua GAME_STATE_UPDATE.*
