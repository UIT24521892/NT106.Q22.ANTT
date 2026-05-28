# NT106 Next Features And UI Guide

Tài liệu này tổng hợp trạng thái hiện tại sau Phase 4, các phần UI nên giữ runtime hoặc chuyển sang panel Unity, và roadmap tính năng tiếp theo. Mục tiêu là giúp cả nhóm làm tiếp mà không vô tình phá các phần gameplay/network đang ổn định.

## 1. Trạng Thái Hiện Tại

Game hiện đã có các phần chính:

- Multiplayer TCP, lobby, room, ready/start và đồng bộ lượt.
- Gameplay gồm roll, move token, buy, rent, build, bankruptcy và game over.
- Chat trong game, popup đất, popup thẻ và player info ở 4 góc.
- `GAME_OVER`, score reward và leaderboard Firebase cơ bản.

Các phần không được tự ý đổi nếu chưa kiểm tra kỹ:

- `BoardPoint_00..31`
- Mapping token movement.
- Logic `LastMoveFromPosition -> LastMoveToPosition -> LastFinalPosition`.
- Logic server authoritative cho dice, money và position.

## 2. UI Runtime Hay Panel Unity

Quyết định tạm thời cho từng UI:

| UI | Giữ Runtime / Panel Unity | Lý do |
|---|---|---|
| `GameOverUI` | Giữ runtime tạm thời | Chỉ hiện cuối trận, ít cần chỉnh scene |
| `Leaderboard` trong `GameOverUI` | Giữ runtime tạm thời | Đang gắn chung với popup game over |
| `GameChatUI` | Nên chuyển sang panel Unity | Cần chỉnh vị trí, kích thước, input, scroll |
| `PlayerInfoLayer` | Giữ panel Unity hiện có | Đã có trong scene, chỉ nên bind/update data |
| `CenterActionLayer` | Giữ panel Unity hiện có | Roll/Buy/EndTurn cần chỉnh bằng Inspector |
| `BoardContainer` | Giữ panel Unity hiện có | Chứa button ô đất, không đổi mapping |
| `BoardTileInfoUI` | Sau này nên chuyển thành panel/prefab Unity | Popup nhiều text, cần chỉnh spacing/font |
| `ChanceCardUI` | Có thể giữ runtime hoặc chuyển prefab sau | Popup độc lập, không gấp |
| `PlayerHandUI` | Nên chuyển panel/prefab khi làm Use Card | Cần click từng thẻ và chỉnh layout |

## 3. Hướng Dẫn Chỉnh UI Thủ Công Trong Unity

### PlayerInfoLayer

Không tạo mới `PlayerInfoLayer`.

Trong `GameScene`:

- Mở `Canvas > Panel_GameScene > PlayerInfoLayer`.
- Giữ 4 vùng player ở 4 góc:
  - Góc trái trên: local player / P1.
  - Góc phải trên: P2.
  - Góc trái dưới: P3.
  - Góc phải dưới: P4.
- Mỗi player panel nên có:
  - Text tên.
  - Text tiền.
  - Text vị trí.
  - Text số đất.
  - Text trạng thái `TURN`, `ACTIVE`, `BANKRUPT`, `DISCONNECTED`.
- Không đổi tên parent `PlayerInfoLayer` nếu script đang tìm theo tên.
- Không đặt panel che lên board hoặc chat.

### CenterActionLayer

Trong `GameScene`:

- Mở `Canvas > Panel_GameScene > CenterActionLayer`.
- Đặt các nút phía dưới màn hình:
  - `Btn_Roll`
  - `Btn_Buy`
  - `Btn_EndTurn`
- Giữ reference trong `GameSceneUIBinder`:
  - `Btn Roll`
  - `Btn Buy`
  - `Btn End Turn`
  - `Txt Game State`
  - `Txt Action Log`
  - `Txt Error`
- Nếu đổi tên object thì phải kéo thả lại trong Inspector.
- Không đổi logic click trong Button nếu đã được `GameSceneUIBinder` bind runtime.

### BoardContainer

Không đổi thứ tự button.

Quy tắc:

- `BoardContainer` dùng để click ô đất và hiển thị popup.
- Không reorder children nếu chưa chắc script map theo thứ tự nào.
- Không đổi `BoardPoint_00..31`.
- Có thể chỉnh:
  - Size button.
  - Alpha image.
  - Raycast target.
  - Vị trí button.
- Sau khi chỉnh phải test:
  - Click ô 0 hiện Start.
  - Click ô 1 hiện Tokyo.
  - Click ô 31 hiện Granada.
  - Token vẫn đứng đúng ô.

### GameChatUI

Mục tiêu là chuyển sang panel Unity sau.

Panel nên tạo:

- `Canvas > Panel_GameScene > ChatPanel`
- Có:
  - `Btn_ChatToggle`
  - `Panel_ChatWindow`
  - `ScrollView_Messages`
  - `Input_Message`
  - `Btn_Send`
  - `Btn_Close`
- Trạng thái mặc định:
  - Chỉ hiện `Btn_ChatToggle`.
  - `Panel_ChatWindow` inactive.
- Script sau này sẽ bind object có sẵn thay vì tự tạo runtime.

### BoardTileInfoUI

Để sau, chưa đổi ngay.

Khi chuyển sang panel:

- Tạo `Panel_TileInfoPopup`.
- Có text:
  - Tên ô.
  - Loại ô.
  - Nhóm màu.
  - Giá mua.
  - Chủ sở hữu.
  - Cấp hiện tại.
  - Rent hiện tại.
  - Bảng rent.
- Có nút:
  - `Btn_Build`
  - `Btn_Close`
- Panel mặc định inactive.
- Script chỉ cập nhật nội dung và bật/tắt.

## 4. Roadmap Tính Năng Tiếp Theo

### Phase 5 - UI Cleanup Không Đổi Logic

Mục tiêu:

- Dọn các UI đang khó nhìn nhưng không đổi gameplay.
- Ưu tiên chuyển chat sang panel scene.
- Giữ token/board mapping nguyên vẹn.

Việc làm:

- Chỉnh `GameChatUI` để ưu tiên tìm `ChatPanel` có sẵn.
- Nếu không tìm thấy thì fallback runtime như hiện tại.
- Chỉnh font/spacing player info nếu cần.
- Không sửa server.

Test:

- Mở/tắt chat được.
- Gửi chat giữa 2 client.
- Bubble chat vẫn hiện đúng player.
- Roll/buy/end turn vẫn hoạt động.

### Phase 6 - Use Card Logic

Mục tiêu:

- Người chơi dùng được thẻ đang giữ.
- Các thẻ giữ trong tay trở thành button.

Packet mới client gửi:

```json
{ "Type": "USE_CARD", "Payload": { "RoomId": "...", "Username": "...", "CardId": "..." } }
```

Server xử lý:

- Validate đúng lượt.
- Validate player thật sự có thẻ.
- Apply effect.
- Clear flag thẻ sau khi dùng.
- Broadcast `GAME_STATE_UPDATE`.

Áp dụng trước cho:

- `ESCAPE_ISLAND`
- `FREE_RENT`
- `FORCE_DOUBLE`
- `FREE_UPGRADE`

Test:

- Player có thẻ thì UI hiện.
- Bấm thẻ gửi packet.
- Server apply đúng effect.
- Thẻ biến mất sau khi dùng.

### Phase 7 - Card Target Selection

Mục tiêu:

- Các thẻ cần chọn mục tiêu không auto target nữa.

Packet:

- Server gửi `REQUEST_CARD_CHOICE`.
- Client gửi `CARD_CHOICE_MADE`.

Áp dụng cho:

- `FLIGHT`
- `FREE_UPGRADE`
- `EARTHQUAKE`
- `POWER_OUTAGE`
- `MOVE_CHAMPIONSHIP`

UI:

- Highlight ô hợp lệ.
- Người chơi click target.
- Có nút cancel nếu action không bắt buộc.

Test:

- Chỉ người đang cần chọn thấy highlight.
- Client khác vẫn xem state bình thường.
- Chọn target xong server apply đúng.

### Phase 8 - Dice Visual

Mục tiêu:

- Demo rõ hơn khi roll.
- Hiện 2 viên xúc xắc và tổng điểm.

Cách làm:

- Tạo `DicePanel` trong `CenterActionLayer`.
- Có `Img_Dice1`, `Img_Dice2`, `Txt_DiceTotal`.
- Khi nhận `GAME_STATE_UPDATE`, cập nhật theo `LastDice1`, `LastDice2`, `LastDiceTotal`.
- Animation đơn giản: đổi số nhanh 0.5 giây rồi dừng ở kết quả server.

Test:

- Dice hiển thị đúng kết quả server.
- Không tự random kết quả client.
- Hai client thấy cùng dice.

### Phase 9 - Demo Hardening

Mục tiêu:

- Chuẩn bị bản demo ổn định.

Việc làm:

- Kiểm tra 2 client.
- Kiểm tra build `.exe`.
- Giảm log debug quá dài trên màn hình.
- Chuẩn bị kịch bản demo:
  - Tạo phòng.
  - Join phòng.
  - Ready/start.
  - Roll/move.
  - Buy/rent.
  - Chat.
  - Chance card.
  - Game over/leaderboard.

Test:

- Unity Editor + `.exe`.
- 2 máy cùng LAN nếu có.
- Reconnect/resume nếu kịp.

## 5. Assumptions

- File markdown này nằm ở root project với tên `NT106_NEXT_FEATURES_AND_UI_GUIDE.md`.
- Chưa chỉnh code/UI ngay trong bước này.
- `PlayerInfoLayer`, `CenterActionLayer`, `BoardContainer` đã tồn tại trong `GameScene` và sẽ được giữ.
- `GameOverUI` và leaderboard cuối trận tiếp tục giữ runtime cho đến khi cần polish cuối.
- Ưu tiên tiếp theo sau file này là Phase 5: cleanup chat UI sang panel scene.
