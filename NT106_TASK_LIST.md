# NT106 Monopoly — Task List

## Tổng quan thứ tự thực hiện

```
Task 1 — Rebalance giá tiền bàn cờ
    ↓
Task 2 — Bankruptcy & Win Condition
    ↓
Task 3 — Packet GAME_OVER & Client kết thúc game
    ↓
Task 4 — Post-match Score & Leaderboard (Firebase)
```

---

## Task 1 — Rebalance giá tiền bàn cờ

**Mục tiêu:** Điều chỉnh giá mua đất, tiền thuê, chi phí xây nhà để game có tension và kéo dài hợp lý.

### Vấn đề hiện tại
- Giá đất quá thấp → người chơi mua đất dễ, không có áp lực tài chính.
- Tiền thuê quá thấp → đất sở hữu không có giá trị chiến lược.
- Game kết thúc quá nhanh hoặc thiếu drama.

### Việc cần làm

#### `Monopoly.Shared/Models/Constants/Constant.cs`
- Điều chỉnh lại toàn bộ `BuyPrice`, `RentPrices[]`, `BuildCosts[]` trong `BoardDatabase.Squares`.
- Chia theo nhóm:

| Nhóm | Ô đất | BuyPrice | Rent base | Rent hotel |
|------|-------|----------|-----------|------------|
| Thấp | Tokyo, Osaka | 100–120 | 10–14 | 250–300 |
| Trung thấp | Paris, Lyon, Nice | 160–200 | 18–24 | 400–500 |
| Trung | New York, Las Vegas, Chicago | 220–260 | 28–36 | 600–700 |
| Trung cao | Sydney, Dubai, London | 280–320 | 40–50 | 800–900 |
| Cao | Berlin, Rome, Milan, Venice | 340–400 | 55–70 | 1000–1100 |
| Cao nhất | Shanghai, Beijing, Madrid, Seville, Granada | 420–500 | 80–100 | 1200–1500 |

- `BuildCosts[]` nên tăng dần theo nhóm, tương đương 50–60% `BuyPrice`.

#### `Monopoly.Server/Program.cs`
- Chỉnh `startingMoney` (tiền khởi đầu) lên khoảng **2000–2500** để phù hợp với mặt bằng giá mới.
- Chỉnh `passGoReward` (lương qua ô Bắt Đầu) lên khoảng **300–400**.
- Chỉnh tiền phạt ô Thuế theo tỷ lệ mới.

### File sửa
- `Monopoly.Shared/Models/Constants/Constant.cs`
- `Monopoly.Server/Program.cs`

### Tiêu chí hoàn thành
- [ ] Giá đất đã điều chỉnh toàn bộ 32 ô.
- [ ] Tiền khởi đầu và lương Start đã cập nhật.
- [ ] Test thử 1 ván đủ 4 người, game kéo dài hợp lý (không quá 15 phút, không quá 1 tiếng).

---

## Task 2 — Bankruptcy & Win Condition

**Mục tiêu:** Xử lý logic phá sản đúng và xác định người thắng cuộc.

### Việc cần làm

#### `Monopoly.Server/RoomModels.cs`
Thêm vào `GamePlayerState`:
```csharp
public bool IsBankrupt { get; set; } = false;
public int BankruptcyOrder { get; set; } = 0; // 1 = phá sản đầu tiên, 2 = thứ hai...
```

#### `Monopoly.Server/Program.cs`

**Xử lý phá sản:**
- Sau mỗi transaction trừ tiền (rent, thuế, phạt thẻ Cơ Hội):
  - Nếu `player.Money < 0` → gọi hàm `HandleBankruptcy(player, room)`.
- Trong `HandleBankruptcy`:
  - Đặt `player.IsBankrupt = true`.
  - Gán `player.BankruptcyOrder` = số thứ tự phá sản hiện tại trong phòng + 1.
  - Trả toàn bộ đất của player về `OwnerId = null`, `HouseCount = 0`.
  - Nếu phá sản do không trả được rent → phần tiền còn lại của player chuyển cho chủ đất.
  - Broadcast `GAME_STATE_UPDATE` để client cập nhật.

**Kiểm tra thắng game:**
- Sau mỗi `HandleBankruptcy`, gọi `CheckWinCondition(room)`.
- Đếm số player còn `IsBankrupt = false`.
- Nếu chỉ còn 1 player sống → gọi `EndGame(room)`.

**Trong `EndGame`:**
- Xác định bảng xếp hạng:
  - Rank 1: người còn sống.
  - Rank 2, 3, 4: theo `BankruptcyOrder` giảm dần (phá sản sau = rank cao hơn).
- Tạo `matchId` = GUID hoặc timestamp.
- Broadcast packet `GAME_OVER` (xem Task 3).
- Reset trạng thái phòng về lobby.

### File sửa
- `Monopoly.Server/RoomModels.cs`
- `Monopoly.Server/Program.cs`

### Tiêu chí hoàn thành
- [ ] Player phá sản bị đánh dấu `IsBankrupt`, đất trả về pool.
- [ ] Thứ tự phá sản được ghi lại đúng.
- [ ] Game kết thúc đúng khi còn 1 người.
- [ ] Rank được xác định đúng thứ tự.

---

## Task 3 — Packet GAME_OVER & Client màn hình kết thúc

**Mục tiêu:** Server broadcast kết quả, client hiện màn hình kết thúc game.

### Việc cần làm

#### Packet mới: `GAME_OVER` (Server → Client)
```json
{
  "Type": "GAME_OVER",
  "Payload": {
    "MatchId": "abc123",
    "Rankings": [
      { "UserId": "u1", "DisplayName": "PlayerA", "Rank": 1 },
      { "UserId": "u2", "DisplayName": "PlayerB", "Rank": 2 },
      { "UserId": "u3", "DisplayName": "PlayerC", "Rank": 3 },
      { "UserId": "u4", "DisplayName": "PlayerD", "Rank": 4 }
    ]
  }
}
```

#### `Monopoly.Client.Unity/Assets/Scripts/NetworkDataModels.cs`
Thêm:
```csharp
public class GameOverData {
    public string MatchId { get; set; }
    public List<RankingEntry> Rankings { get; set; }
}

public class RankingEntry {
    public string UserId { get; set; }
    public string DisplayName { get; set; }
    public int Rank { get; set; }
    public int ScoreEarned { get; set; } // điền sau ở Task 4
}
```

#### `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs`
- Xử lý case `GAME_OVER` trong switch packet.
- Fire event: `public static event Action<GameOverData> OnGameOver`.

#### `Monopoly.Client.Unity/Assets/Scripts/GameOverUI.cs` ← file mới
- Subscribe `NetworkManager.OnGameOver`.
- Hiện panel modal với bảng:

```
🏆 Kết thúc trận!

  Hạng  |  Tên người chơi  
  1st   |  PlayerA        
  2nd   |  PlayerB         
  3rd   |  PlayerC         
  4th   |  PlayerD         

  [Về Lobby]
```

- Nút "Về Lobby" gửi packet `LEAVE_ROOM` và load scene Lobby.

### File sửa/tạo
- `Monopoly.Server/Program.cs` (broadcast GAME_OVER)
- `Monopoly.Client.Unity/Assets/Scripts/NetworkDataModels.cs`
- `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs`
- `Monopoly.Client.Unity/Assets/Scripts/GameOverUI.cs` ← mới

### Tiêu chí hoàn thành
- [ ] Server broadcast `GAME_OVER` đúng sau khi game kết thúc.
- [ ] Client hiện bảng xếp hạng cuối trận.
- [ ] Nút "Về Lobby" hoạt động đúng.

---

## Task 4 — Post-match Score & Leaderboard (Firebase)

> **Yêu cầu:** Task 2 và Task 3 phải hoàn thành trước.

**Mục tiêu:** Tích lũy điểm sau mỗi trận và hiện bảng xếp hạng toàn server.

### Quy tắc tính điểm

| Hạng | Điểm thưởng |
|------|------------|
| 🥇 1st | +100 |
| 🥈 2nd | +50 |
| 🥉 3rd | +20 |
| 4th | +5 |

### Firebase Data Structure
```
/users
  /{userId}
    displayName: "PlayerA"
    score: 1250
    totalMatches: 15
    wins: 6
    /matchHistory
      /{matchId}
        rank: 1
        scoreEarned: 100
        playedAt: "2025-01-01T10:00:00Z"
```

### Việc cần làm

#### `Monopoly.Server/FirebaseApiService.cs`
Thêm 2 method:

**`UpdatePlayerMatchResult(string userId, int rank, string matchId)`:**
- Tính `scoreEarned` theo bảng quy tắc.
- PATCH `/users/{userId}`: tăng `score`, tăng `totalMatches`, tăng `wins` nếu rank == 1.
- POST `/users/{userId}/matchHistory/{matchId}`: lưu `rank`, `scoreEarned`, `playedAt`.

**`GetLeaderboard(int limit = 10)`:**
- GET Firebase với query `orderBy="score"&limitToLast={limit}`.
- Parse và trả về `List<LeaderboardEntry>` đã sort giảm dần theo score.

#### Model mới
```csharp
public class LeaderboardEntry {
    public string DisplayName { get; set; }
    public int Score { get; set; }
    public int Wins { get; set; }
    public int TotalMatches { get; set; }
}
```

#### `Monopoly.Server/Program.cs`
- Trong `EndGame`, sau khi broadcast `GAME_OVER`:
  - Gọi `UpdatePlayerMatchResult` cho từng player theo rank.
- Xử lý packet `GET_LEADERBOARD` từ client:
  - Gọi `GetLeaderboard()`.
  - Gửi lại packet `LEADERBOARD_DATA` cho client đó.

#### Packet mới: `GET_LEADERBOARD` (Client → Server)
```json
{ "Type": "GET_LEADERBOARD", "Payload": {} }
```

#### Packet mới: `LEADERBOARD_DATA` (Server → Client)
```json
{
  "Type": "LEADERBOARD_DATA",
  "Payload": {
    "Entries": [
      { "Rank": 1, "DisplayName": "PlayerA", "Score": 1250, "Wins": 6, "TotalMatches": 15 }
    ]
  }
}
```

#### `Monopoly.Client.Unity/Assets/Scripts/NetworkDataModels.cs`
Thêm:
- `LeaderboardData` — deserialize `LEADERBOARD_DATA`
- Cập nhật `RankingEntry` thêm field `ScoreEarned`

#### `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs`
- Xử lý case `LEADERBOARD_DATA` → fire event `OnLeaderboardReceived`.
- Thêm method `RequestLeaderboard()` gửi packet `GET_LEADERBOARD`.

#### `Monopoly.Client.Unity/Assets/Scripts/GameOverUI.cs`
- Cập nhật bảng kết thúc trận thêm cột **Điểm thưởng**.
- Thêm nút "Bảng Xếp Hạng".
- Khi bấm: gọi `NetworkManager.RequestLeaderboard()`, chờ event, hiện panel BXH:

```
🏆 Bảng Xếp Hạng Toàn Server

  #   |  Tên          |  Điểm  |  Thắng  
  1   |  PlayerA      |  1250  |  6      
  2   |  PlayerB      |  980   |  4      
  ...

  [Đóng]  [Về Lobby]
```

### File sửa/tạo
- `Monopoly.Server/FirebaseApiService.cs`
- `Monopoly.Server/Program.cs`
- `Monopoly.Client.Unity/Assets/Scripts/NetworkDataModels.cs`
- `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs`
- `Monopoly.Client.Unity/Assets/Scripts/GameOverUI.cs`

### Tiêu chí hoàn thành
- [ ] Score được cập nhật lên Firebase sau mỗi trận.
- [ ] `matchHistory` lưu đúng cho từng player.
- [ ] BXH hiển thị top 10 đúng thứ tự.
- [ ] Điểm thưởng hiển thị trong màn hình kết thúc trận.

---

## Tổng hợp file cần sửa

| File | Task |
|------|------|
| `Monopoly.Shared/Models/Constants/Constant.cs` | Task 1 |
| `Monopoly.Server/Program.cs` | Task 1, 2, 3, 4 |
| `Monopoly.Server/RoomModels.cs` | Task 2 |
| `Monopoly.Server/FirebaseApiService.cs` | Task 4 |
| `NetworkDataModels.cs` | Task 3, 4 |
| `NetworkManager.cs` | Task 3, 4 |
| `GameOverUI.cs` ← **mới** | Task 3, 4 |

---

## Những phần KHÔNG sửa

- Mapping `BoardPoint_00..31` trong GameScene.
- `BoardTokenManager` và logic di chuyển token.
- Logic `LastMoveFromPosition → LastMoveToPosition → LastFinalPosition`.
- Các packet type hiện có (chỉ thêm mới, không đổi cũ).
