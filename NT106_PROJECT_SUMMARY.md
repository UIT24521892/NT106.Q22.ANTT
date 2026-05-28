# Tổng hợp project game NT106 - Monopoly Network Game

## 1. Tổng quan

Project là game Monopoly/Business Tour nhiều người chơi, phục vụ đồ án môn NT106 - Lập trình mạng.

Mô hình hiện tại:

- Client: Unity 2022.3, giao diện game, lobby, bàn cờ, token, popup, chat.
- Server: .NET TCP server, xử lý phòng chơi, lượt chơi, xúc xắc, tiền, đất, thẻ, đồng bộ state.
- Shared data: database ô bàn cờ và thẻ Cơ Hội dùng chung cho logic server.

Giao tiếp mạng:

- TCP socket.
- Packet JSON theo format:

```json
{
  "Type": "PACKET_TYPE",
  "Payload": {}
}
```

- Mỗi packet kết thúc bằng delimiter `<EOF>`.
- Server authoritative: client gửi yêu cầu, server kiểm tra và broadcast `GAME_STATE_UPDATE`.

## 2. Cấu trúc module chính

### Server

Thư mục chính:

- `Monopoly.Server/`

File quan trọng:

- `Monopoly.Server/Program.cs`
  - TCP listener.
  - Parse packet JSON.
  - Xử lý login/lobby/gameplay.
  - Logic roll, turn, buy, build, rent, chat, thẻ Cơ Hội, Đảo Hoang, World Tour, Championship.
  - Broadcast `GAME_STATE_UPDATE`, `CARD_DRAWN`, `CHAT_MESSAGE`.

- `Monopoly.Server/RoomModels.cs`
  - Model server-side:
    - `Room`
    - `ClientConnection`
    - `GameState`
    - `GamePlayerState`
    - `GamePropertyState`
  - Chứa các field trạng thái mới như:
    - `LastMoveFromPosition`
    - `LastMoveToPosition`
    - `LastFinalPosition`
    - `WorldChampionshipPosition`
    - `SkipTurnsLeft`
    - `IsOnIsland`
    - các flag thẻ đang giữ.

- `Monopoly.Server/FirebaseApiService.cs`
  - Kết nối Firebase REST API cho login/register/profile.

- `Monopoly.Server/GameLogic/DeckManager.cs`
  - Quản lý rút thẻ Cơ Hội từ `CardDatabase`.

### Shared

Thư mục chính:

- `Monopoly.Shared/`

File quan trọng:

- `Monopoly.Shared/Models/Constants/Constant.cs`
  - `BoardDatabase.Squares`: database 32 ô bàn cờ.
  - `CardDatabase.Cards`: database thẻ Cơ Hội.
  - Mỗi ô có:
    - `PositionIndex`
    - `Name`
    - `Type`
    - `ColorSet`
    - `BuyPrice`
    - `RentPrices`
    - `BuildCosts`

Mapping bàn cờ hiện tại:

```text
00 Bắt Đầu
01 Tokyo
02 Cơ quan Thuế
03 Osaka
04 Cơ Hội
05 Paris
06 Lyon
07 Nice
08 Du Lịch Thế Giới
09 New York
10 Las Vegas
11 Chicago
12 Cơ Hội
13 Sydney
14 Dubai
15 London
16 Giải Vô Địch
17 Berlin
18 Cyprus
19 Hamburg
20 Cơ Hội
21 Rome
22 Milan
23 Venice
24 Đảo Hoang
25 Shanghai
26 Beijing
27 Hong Kong
28 Bali
29 Madrid
30 Seville
31 Granada
```

### Unity Client

Thư mục chính:

- `Monopoly.Client.Unity/Assets/Scripts/`

File quan trọng:

- `NetworkManager.cs`
  - Kết nối TCP tới server.
  - Gửi packet gameplay:
    - roll
    - end turn
    - buy
    - build
    - chat
    - resume game
  - Nhận packet:
    - `GAME_STATE_UPDATE`
    - `CARD_DRAWN`
    - `CHAT_MESSAGE`
    - các packet lobby/auth.

- `NetworkDataModels.cs`
  - Model dữ liệu client nhận từ server:
    - `GameStateData`
    - `GamePlayerStateData`
    - `GamePropertyStateData`
    - `ChatMessageData`

- `GameSceneUIBinder.cs`
  - Bind các nút trong GameScene với `NetworkManager`.
  - Khởi tạo các UI runtime:
    - `BoardTokenManager`
    - `GameChatUI`
    - `BoardTileInfoUI`
    - `PlayerHandUI`
    - `PlayerInfoLayerUI`

- `BoardTokenManager.cs`
  - Vẽ token người chơi.
  - Di chuyển token theo `LastMoveFromPosition -> LastMoveToPosition -> LastFinalPosition`.
  - Ưu tiên dùng marker `BoardPoint_00..31`.
  - Giữ nguyên mapping marker hiện tại vì đã chỉnh đúng trong Unity.

- `BoardTileInfoUI.cs`
  - Popup thông tin ô đất.
  - Tạo vùng click runtime theo `BoardPoint_00..31`.
  - Hiển thị giá mua, chủ sở hữu, rent, cấp nhà/khách sạn.
  - Gửi `BUILD_PROPERTY` khi nâng cấp.

- `GameChatUI.cs`
  - Chat trong phòng game.
  - Mặc định thu gọn thành nút `Chat`.
  - Bấm `Chat` để mở panel, bấm `X` để đóng.
  - Hiện bubble chat nổi gần token người gửi.

- `ChanceCardUI.cs`
  - Popup thẻ Cơ Hội khi nhận packet `CARD_DRAWN`.
  - Hiển thị loại thẻ, tên thẻ, người rút, mô tả effect.

- `PlayerHandUI.cs`
  - Hiển thị thẻ người chơi đang giữ.
  - Chỉ hiện khi local player có thẻ giữ trong tay.

- `PlayerInfoLayerUI.cs`
  - HUD 4 góc hiển thị thông tin người chơi.
  - Tiền, vị trí, số đất, trạng thái lượt.

- `LobbyManager.cs`
  - UI lobby, tạo phòng, vào phòng, ready/start/leave.

- `AuthManager.cs`
  - Login/register/profile.

## 3. Luồng chạy chính

### Login và lobby

1. Client kết nối TCP tới server.
2. Người chơi login/register.
3. Vào lobby.
4. Tạo phòng hoặc join phòng.
5. Người chơi ready.
6. Host start game.
7. Server tạo `GameState`.
8. Client load `GameScene`.

### Gameplay

1. Client hiện nút Roll, Buy, End Turn theo state server.
2. Người chơi bấm Roll.
3. Client gửi request roll.
4. Server random dice bằng `RandomNumberGenerator`.
5. Server xử lý:
   - vị trí mới
   - qua Start nhận tiền
   - trả rent nếu vào đất người khác
   - ô đặc biệt
   - thẻ Cơ Hội
   - phá sản/thắng trận
6. Server broadcast `GAME_STATE_UPDATE`.
7. Client cập nhật UI, token animation, popup, HUD.
8. Người chơi có thể mua/nâng cấp nếu hợp lệ.
9. Người chơi bấm End Turn.
10. Server chuyển lượt.

## 4. Tính năng hiện tại

### Network và phòng chơi

- TCP client-server.
- Packet JSON có `Type` và `Payload`.
- Hỗ trợ nhiều client trong cùng phòng.
- Tạo phòng, join phòng.
- Ready/start game.
- Leave room.
- Reconnect/resume game cơ bản.
- Timeout lượt.
- Server broadcast state cho toàn bộ client.

### Gameplay Monopoly

- Bàn cờ 32 ô.
- Roll 2 xúc xắc.
- Di chuyển token từng ô theo server state.
- Qua ô Start nhận tiền.
- Mua đất.
- Trả tiền thuê khi vào đất người khác.
- Nâng cấp đất bằng `BUILD_PROPERTY`.
- Nhà/khách sạn ảnh hưởng tiền thuê.
- Theo dõi tiền, vị trí, chủ sở hữu đất.
- Xử lý phá sản và kết thúc trận.

### Ô đặc biệt

- Tax:
  - Trừ tiền khi vào ô Thuế.

- Chance:
  - Rút thẻ từ `CardDatabase`.
  - Broadcast `CARD_DRAWN`.
  - Apply effect cơ bản.

- Lost Island:
  - Vào Đảo Hoang.
  - Có 3 lượt để lắc đôi thoát.
  - Nếu lắc đôi thì thoát và đi tiếp.
  - Nếu hết lượt thì trả tiền để ra đảo.

- World Tour:
  - Vào ô Du Lịch Thế Giới sẽ bị skip lượt kế tiếp.

- World Championship:
  - Người vào ô Giải Vô Địch thu tiền từ các đối thủ.
  - Có field `WorldChampionshipPosition` để hỗ trợ dời vị trí về sau.

### Thẻ Cơ Hội

Đã có các effect cơ bản:

- `FINE`
- `JACKPOT`
- `GO_TO_JAIL`
- `SKIP_TURN`
- `TAX_PENALTY`
- `CHARITY_PAY`
- `GO_TO_WORLD_TOUR`
- `FREE_RENT`
- `FLIGHT`
- `ESCAPE_ISLAND`
- `FREE_UPGRADE`
- `FORCE_DOUBLE`
- `EARTHQUAKE`
- `POWER_OUTAGE`
- `MOVE_CHAMPIONSHIP`

Lưu ý:

- Một số thẻ cần chọn target hiện đang auto-target tạm thời.
- UI chọn target chưa làm.
- UI dùng thẻ chủ động chưa làm.

### Unity UI

- Token player trên bàn cờ.
- Marker `BoardPoint_00..31` đã dùng làm nguồn chính cho token và popup ô đất.
- Popup thông tin ô đất.
- Popup thẻ Cơ Hội.
- HUD player ở 4 góc.
- Chat trong game:
  - mở/đóng bằng nút Chat.
  - bubble nổi gần player.
- Player hand UI:
  - hiển thị khi người chơi có thẻ giữ trong tay.

## 5. Các packet quan trọng

### Client gửi server

- `CREATE_ROOM`
- `JOIN_ROOM`
- `LEAVE_ROOM`
- `START_GAME`
- `DiceRoll` / `DICE_ROLL`
- `EndTurn` / `END_TURN`
- `BuyProperty` / `BUY_PROPERTY`
- `BUILD_PROPERTY`
- `GAME_CHAT`
- `RESUME_GAME`

### Server gửi client

- `GAME_STARTING`
- `GAME_STATE_UPDATE`
- `GAME_ACTION_FAILED`
- `CARD_DRAWN`
- `CHAT_MESSAGE`
- `ROOM_CLOSED`
- `RESUME_GAME_NONE`

## 6. Những phần cần giữ nguyên

Các phần sau đã ổn và không nên đổi nếu không cần thiết:

- Mapping `BoardPoint_00..31` trong GameScene.
- `BoardTokenManager` dùng `BoardPoint_00..31`.
- `BoardTileInfoUI` dùng marker để tạo vùng click.
- Logic `LastMoveFromPosition -> LastMoveToPosition -> LastFinalPosition`.
- Cách token đi theo từng ô.

## 7. Vấn đề/tồn tại hiện tại

- Một số UI đang tạo runtime bằng code, nên khó chỉnh màu/font bằng Inspector.
- Popup đất đổi màu chữ theo ColorSet chưa ổn định, nên nên để task polish cuối.
- `PlayerHandUI` chỉ hiện khi có thẻ giữ trong tay, nên khi test rút thẻ tiền/phạt sẽ không thấy panel.
- Các thẻ cần chọn target vẫn đang auto-target.
- Chưa có UI để dùng thẻ chủ động.
- Chưa có hiệu ứng dice trực quan.
- Chưa có lịch sử log đẹp, log hiện vẫn hơi kỹ thuật.
- Chưa có prefab UI chuẩn cho chat/card/player hand.
- Chưa có test tự động cho server game logic.

## 8. Tính năng nên cải thiện

### Ưu tiên cao

1. Use Card Logic
   - Cho phép người chơi dùng thẻ đang giữ.
   - Các thẻ cần xử lý:
     - `ESCAPE_ISLAND`
     - `FLIGHT`
     - `FREE_UPGRADE`
     - `FORCE_DOUBLE`
     - `FREE_RENT`

2. Card Target Selection
   - Server gửi `REQUEST_CARD_CHOICE`.
   - Client highlight các ô hợp lệ.
   - Client gửi `CARD_CHOICE_MADE`.
   - Áp dụng cho:
     - `EARTHQUAKE`
     - `POWER_OUTAGE`
     - `MOVE_CHAMPIONSHIP`
     - `FLIGHT`
     - `FREE_UPGRADE`

3. UI tay bài có thể bấm
   - `PlayerHandUI` hiện tại chỉ hiển thị.
   - Cần biến mỗi thẻ thành button.
   - Button gửi packet dùng thẻ lên server.

### Ưu tiên vừa

4. Dice UI
   - Hiển thị 2 viên xúc xắc.
   - Có animation roll.
   - Dễ demo hơn với thầy.

5. Làm popup đất thành prefab/scene object thật
   - Cho phép chỉnh font, màu, layout bằng Inspector.
   - Dễ bảo trì hơn runtime UI.

6. Log sự kiện đẹp hơn
   - Tách log mới nhất và lịch sử.
   - Có màu cho tiền tăng/giảm.
   - Có icon cho rent, buy, card, jail.

7. Hiển thị trạng thái đặc biệt trên HUD
   - Đang ở Đảo Hoang.
   - Còn mấy lượt skip.
   - Có thẻ đang giữ.
   - Đang disconnect.

### Nâng cao/cộng điểm

8. Spectator mode
   - Cho phép người ngoài vào xem trận.

9. Save/load match state
   - Server lưu state phòng.
   - Người chơi reconnect sau khi tắt app.

10. Match history
   - Lưu lịch sử trận, người thắng, thời gian, số lượt.

11. Anti-cheat rõ ràng hơn
   - Client không tự quyết định dice/money/position.
   - Server validate mọi action.
   - Ghi log server cho action bất thường.

12. Server unit test
   - Test roll.
   - Test rent.
   - Test chance card.
   - Test island.
   - Test championship.

## 9. Roadmap đề xuất tiếp theo

### Phase 7 - Use Card Logic

Mục tiêu:

- Người chơi bấm được thẻ trong `PlayerHandUI`.
- Client gửi packet `USE_CARD`.
- Server validate và apply effect.

File có thể sửa:

- `Monopoly.Server/Program.cs`
- `Monopoly.Client.Unity/Assets/Scripts/PlayerHandUI.cs`
- `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs`

### Phase 8 - Target Selection

Mục tiêu:

- Với thẻ cần chọn mục tiêu, server không auto-target nữa.
- Client hiển thị ô hợp lệ.
- Người chơi click chọn target.

File có thể sửa:

- `Program.cs`
- `NetworkManager.cs`
- `BoardTileInfoUI.cs`
- `PlayerHandUI.cs`

### Phase 9 - UI polish

Mục tiêu:

- Chuyển popup đất/card/hand thành prefab hoặc object trong scene.
- Cho phép chỉnh màu/font bằng Inspector.
- Làm UI demo đẹp và ổn định hơn.

File/Unity cần chỉnh:

- `GameScene.unity`
- `BoardTileInfoUI.cs`
- `ChanceCardUI.cs`
- `PlayerHandUI.cs`
- Có thể cần prefab mới.

### Phase 10 - Demo hardening

Mục tiêu:

- Fix lỗi nhỏ trước demo.
- Build lại `.exe`.
- Test 2 client.
- Chuẩn bị kịch bản demo.

## 10. Test case demo đề xuất

### Test 1 - Join phòng và start

1. Mở server.
2. Mở Unity Editor client 1.
3. Mở `.exe` client 2.
4. Client 1 tạo phòng.
5. Client 2 join phòng.
6. Ready/start game.

Kỳ vọng:

- Cả hai client vào cùng GameScene.
- HUD hiển thị đủ player.
- Current turn đồng bộ.

### Test 2 - Roll và di chuyển token

1. Player roll.
2. Quan sát token đi từng ô.
3. Client còn lại thấy token di chuyển.

Kỳ vọng:

- Token đi đúng số ô theo dice.
- Không nhảy sai vị trí.
- Popup đất đúng với vị trí.

### Test 3 - Mua đất và trả rent

1. Player A vào đất chưa chủ và mua.
2. Player B vào đất đó.

Kỳ vọng:

- Tiền Player B giảm.
- Tiền Player A tăng.
- Owner property đồng bộ trên cả hai client.

### Test 4 - Nâng cấp đất

1. Player vào đất mình sở hữu.
2. Bấm nâng cấp.

Kỳ vọng:

- Tiền giảm đúng.
- House/hotel tăng.
- Rent hiện tại tăng.

### Test 5 - Cơ Hội

1. Player vào ô Cơ Hội.
2. Server rút thẻ.

Kỳ vọng:

- Popup thẻ hiện trên cả hai client.
- Effect được apply vào money/position/skip/card flag.

### Test 6 - Đảo Hoang

1. Player vào ô Đảo Hoang hoặc rút thẻ đi đảo.
2. Tới lượt player đó roll.

Kỳ vọng:

- Nếu ra đôi thì thoát đảo và đi tiếp.
- Nếu không ra đôi thì giảm lượt chờ.
- Hết lượt thì trả tiền ra đảo.

### Test 7 - World Tour

1. Player vào ô Du Lịch Thế Giới.
2. Tới lượt sau bấm Roll.

Kỳ vọng:

- Player bị skip lượt.
- Log hiển thị lý do.

### Test 8 - Championship

1. Player vào ô Giải Vô Địch.

Kỳ vọng:

- Player thu tiền từ các đối thủ.
- Tiền đồng bộ trên cả hai client.

### Test 9 - Chat

1. Bấm nút Chat.
2. Gửi tin nhắn.
3. Bấm X để thu gọn.

Kỳ vọng:

- Khung chat mở/đóng được.
- Bubble hiện gần token người gửi.
- Client còn lại nhận được tin nhắn.

## 11. Ghi chú cho báo cáo môn NT106

Các yếu tố lập trình mạng thể hiện rõ:

- TCP socket client-server.
- JSON protocol tự định nghĩa.
- Server authoritative game state.
- Multi-client synchronization.
- Room/lobby management.
- Realtime turn state broadcast.
- Chat realtime trong game.
- Reconnect/resume cơ bản.
- Validate action ở server.

Điểm nên nhấn mạnh khi demo:

- Client không tự quyết định kết quả xúc xắc, tiền, vị trí.
- Server xử lý toàn bộ logic và gửi state về client.
- Hai client nhìn thấy cùng một trạng thái sau mỗi action.
- Chat và popup thẻ là các event realtime riêng ngoài `GAME_STATE_UPDATE`.
