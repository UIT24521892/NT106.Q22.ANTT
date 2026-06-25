# NT106 Feature Status And Task Plan

Tài liệu này tổng hợp tính năng hiện có, các phần cần cải thiện và các tính năng cần bổ sung cho project Monopoly NT106. Mục tiêu là giúp team chia task rõ ràng mà không làm hỏng những phần gameplay/network đang ổn định.

File này không thay thế `NT106_NEXT_FEATURES_AND_UI_GUIDE.md`; đây là bản checklist task cụ thể hơn để tiếp tục implement và test.

## 1. Tính Năng Đã Có

### Multiplayer TCP

- Đăng nhập và đăng ký tài khoản.
- Lobby và danh sách phòng.
- Tạo phòng và join phòng.
- Ready/start game.
- Đồng bộ state game từ server về client.

### Gameplay

- Roll dice theo server authoritative.
- Di chuyển token theo chuỗi state `LastMoveFromPosition -> LastMoveToPosition -> LastFinalPosition`.
- Mua đất.
- Tính tiền thuê.
- Xây nhà/khách sạn.
- Xử lý phá sản.
- Game over khi trận đấu kết thúc.

### UI Hiện Có

- `PlayerInfoLayer`: hiện thông tin người chơi quanh board.
- `CenterActionLayer`: roll/buy/end turn/action log.
- `BoardContainer`: chứa các button ô bàn cờ.
- `BoardTileInfoUI`: popup thông tin ô đất.
- `ChanceCardUI`: popup thẻ cơ hội.
- `GameChatUI`: chat trong game.
- `PlayerHandUI`: hiện bài trên tay người chơi.
- `GameOverUI`: popup game over và ranking.
- `DiceVisualUI`: hiện mặt xúc xắc.
- `PropertyBuildMarkerUI`: marker nhà/khách sạn trên ô đất.

### Firebase Và Leaderboard Cơ Bản

- Cộng score/reward cuối trận.
- Lấy leaderboard Firebase để hiện ở màn hình cuối trận.

### Không Được Tự Ý Đổi

- Không đổi tên, thứ tự hoặc ý nghĩa các object `BoardPoint_00..31`.
- Không đổi mapping token movement nếu chưa test lại toàn bộ di chuyển trên board.
- Không đổi chuỗi state di chuyển `LastMoveFromPosition -> LastMoveToPosition -> LastFinalPosition`.
- Không chuyển dice/money/position sang client-authoritative; server vẫn phải là nguồn đúng.
- Không đổi thứ tự board tile/button nếu chưa cập nhật tất cả mapping liên quan.

## 2. Tính Năng Cần Cải Thiện

| Task | Ưu tiên | Mục tiêu |
|---|---:|---|
| UI-01 Chat UI polish | P2 | Chuẩn hóa layout chat để demo sạch, không che board và không chồng UI. |
| UI-02 Dice visual polish | P2 | Xúc xắc hiện đúng mặt, đúng vị trí trung tâm và không hiện placeholder xấu. |
| UI-03 Board tile popup polish | P1 | Chuyển popup thông tin ô đất sang panel/prefab để dễ design và dễ test build. |
| UI-04 Chance card popup polish | P1 | Làm popup thẻ rõ ràng, đọc được text dài và đóng/mở ổn định. |
| UI-05 Game over + leaderboard polish | P1 | Hiện winner/ranking/score/leaderboard rõ hơn và có đường về lobby/menu. |
| DEMO-01 Giảm log/debug trên màn hình | P2 | Giữ màn hình demo gọn, chỉ hiện lỗi cần thiết. |

### UI-01 Chat UI Polish

**Mục tiêu:** Chat trong game cần gọn, dễ đọc, không che board và không tạo nhiều panel trùng nhau.

**File cần sửa:**

- `Monopoly.Client.Unity/Assets/Scripts/GameChatUI.cs`
- `Monopoly.Client.Unity/Assets/Resources/UI/ChatPanel.prefab`

**Unity cần chỉnh:**

- Prefab `ChatPanel`.
- `Btn_ChatToggle`.
- `Panel_ChatWindow`.
- `ScrollView_Messages`.
- `Input_Message`.
- `Btn_Send`.
- `Btn_Close`.

**Việc làm:**

- Chuẩn hóa anchor, size, background và spacing của prefab chat.
- Bỏ panel chat thừa trong scene nếu đã dùng prefab từ `Resources/UI/ChatPanel`.
- Đảm bảo open/close chat không chồng lên action buttons, board tile popup hoặc game over popup.
- Bubble chat nên hiện đúng username/token color nếu state đã có dữ liệu.

**Test cần chạy:**

- Mở/tắt chat nhiều lần trong game.
- Gửi tin nhắn giữa 2 client.
- Tin nhắn hiện đúng người gửi và bubble/token color.
- Chat không che nút Roll/Buy/EndTurn và không làm input mất focus bất thường.

### UI-02 Dice Visual Polish

**Mục tiêu:** Dice panel nằm đúng vùng trung tâm board, hiện đúng 2 mặt xúc xắc và tổng điểm.

**File cần sửa:**

- `Monopoly.Client.Unity/Assets/Scripts/DiceVisualUI.cs`
- `Monopoly.Client.Unity/Assets/Scripts/GameSceneUIBinder.cs` nếu cần bind thêm reference.

**Unity cần chỉnh:**

- `Canvas > Panel_GameScene > CenterActionLayer > DicePanel`.
- `Img_Dice1`.
- `Img_Dice2`.
- `Txt_DiceTotal`.
- `Monopoly.Client.Unity/Assets/Resources/DiceFaces/dice-1.png` đến `dice-6.png`.

**Việc làm:**

- Chỉnh vị trí `DicePanel` vào giữa vùng xanh trong board.
- Chỉnh size 2 dice face để rõ nhưng không che token/action button.
- Chỉnh `Txt_DiceTotal` nằm phía trên hoặc gần dice theo layout thống nhất.
- Ẩn placeholder trước khi có kết quả roll; không hiện dấu `-` trên panel.

**Test cần chạy:**

- Server roll ra số nào thì 2 mặt dice hiện đúng số đó.
- 2 client trong cùng phòng thấy cùng kết quả dice.
- Trước khi roll, panel không hiện placeholder `-`.
- Khi sang turn mới, dice reset/hiện theo đúng logic hiện tại.

### UI-03 Board Tile Popup Polish

**Mục tiêu:** Popup thông tin ô đất cần dễ đọc, dễ thao tác build và vẫn giữ nguyên mapping board.

**File cần sửa:**

- `Monopoly.Client.Unity/Assets/Scripts/BoardTileInfoUI.cs`

**Unity cần chỉnh:**

- Tạo `Panel_TileInfoPopup` trong `GameScene` hoặc prefab riêng nếu cần.
- Text tên ô, loại ô, giá, owner, cấp xây dựng, bảng rent.
- `Btn_Build`.
- `Btn_Close`.
- Giữ nguyên các button `BoardPoint_00..31`.

**Việc làm:**

- Chuyển runtime popup sang panel/prefab để design bằng Inspector.
- Chỉnh spacing, font size, background và scroll nếu rent table dài.
- Build button chỉ hiện khi player đang có quyền build trên city của mình.
- Không đổi click handler/mapping của `BoardPoint_00..31`.

**Test cần chạy:**

- Click ô 0 `Start`.
- Click ô 1 `Tokyo`.
- Click ô 31 `Granada`.
- Popup hiện đúng type/gia/owner/rent.
- `Btn_Build` chỉ hiện đúng đất city của owner và đúng điều kiện build.

### UI-04 Chance Card Popup Polish

**Mục tiêu:** Popup thẻ cơ hội cần rõ loại thẻ, tên thẻ và effect, đọc được text dài.

**File cần sửa:**

- `Monopoly.Client.Unity/Assets/Scripts/ChanceCardUI.cs`

**Unity cần chỉnh:**

- Có thể tạo prefab `ChanceCardPanel`.
- Background/card frame.
- Text tên thẻ.
- Text loại thẻ.
- Text effect.
- Nút close nếu không auto hide.

**Việc làm:**

- Chuyển popup thẻ sang prefab nếu muốn design bằng ảnh/card frame.
- Đảm bảo text dài wrap đúng và không chồng lên nút.
- Giữ auto hide/close theo flow hiện tại nếu gameplay đang phụ thuộc vào timing.

**Test cần chạy:**

- Rút thẻ Golden.
- Rút thẻ Silver.
- Rút thẻ Wooden.
- Effect text dài vẫn đọc được.
- Popup auto hide hoặc close đúng, không kẹt input sau khi đóng.

### UI-05 Game Over + Leaderboard Polish

**Mục tiêu:** Cuối trận cần hiện winner, ranking, score và leaderboard rõ, đồng thời có nút quay về lobby/menu.

**File cần sửa:**

- `Monopoly.Client.Unity/Assets/Scripts/GameOverUI.cs`
- `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs` nếu cần chỉnh cách request/nhận leaderboard.

**Unity cần chỉnh:**

- Có thể tạo prefab/panel game over để thay runtime UI.
- Text winner.
- List ranking trong trận.
- List leaderboard Firebase.
- `Btn_BackToLobby` hoặc `Btn_MainMenu`.

**Việc làm:**

- Hiện winner/ranking/score/leaderboard theo layout dễ đọc.
- Thêm nút về lobby/menu và reset state UI cần thiết.
- Đảm bảo leaderboard request không bị gọi lặp vô hạn hoặc hiện dữ liệu cũ sai trận.

**Test cần chạy:**

- Game over hiện đúng winner.
- Ranking và score cuối trận đúng với state server.
- Leaderboard Firebase trả về và render đúng.
- Quay về lobby/menu không kẹt room, turn state hoặc scene state.

### DEMO-01 Giảm Log/Debug Trên Màn Hình

**Mục tiêu:** Màn hình demo cần sạch hơn nhưng vẫn giữ đủ thông tin lỗi quan trọng.

**File cần sửa:**

- `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs`
- `Monopoly.Client.Unity/Assets/Scripts/BoardTokenManager.cs`
- `Monopoly.Client.Unity/Assets/Scripts/BoardTileInfoUI.cs`
- `Monopoly.Client.Unity/Assets/Scripts/LobbyManager.cs`

**Unity cần chỉnh:**

- Thu gọn `Txt_ActionLog`.
- Giảm text overlay đỏ nếu demo cần sạch.
- Kiểm tra vị trí `Txt_Error` để không che board.

**Việc làm:**

- Phân loại log nào chỉ cần `Debug.Log`, log nào cần hiện UI.
- Rút gọn action log trên màn hình, ưu tiên thông báo người chơi cần biết.
- Giữ lỗi network/gameplay nghiêm trọng để demo vẫn debug được.

**Test cần chạy:**

- Chơi 2 client trong 3-5 turn và kiểm tra log không che board.
- Lỗi đăng nhập/join room/roll sai turn vẫn hiện rõ.
- Console vẫn có log cần thiết để debug.

## 3. Tính Năng Cần Bổ Sung

| Task | Ưu tiên | Mục tiêu |
|---|---:|---|
| CARD-01 Use Card Logic | P0 | Cho người chơi bấm thẻ trên tay và server validate/apply effect. |
| CARD-02 Card Target Selection | P0 | Hỗ trợ chọn target cho các thẻ cần chọn ô đất/người chơi/đích đến. |
| RESUME-01 Reconnect/Resume UI | P1 | Hoàn thiện UI resume để vào lại trận đang dở. |
| BUILD-01 Build executable hardening | P2 | Chuẩn hóa settings và checklist build `.exe` cho demo 2 client. |

### CARD-01 Use Card Logic

**Mục tiêu:** Player có thể dùng thẻ trên tay. Client chỉ gửi request, server validate quyền sở hữu/lượt/effect và broadcast state mới.

**File cần sửa:**

- `Monopoly.Server/Network/PacketRouter.cs`
- `Monopoly.Server/Handles/GameHandler.cs`
- `Monopoly.Server/GameLogic/GameEngine.cs`
- `Monopoly.Client.Unity/Assets/Scripts/PlayerHandUI.cs`
- `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs`
- `Monopoly.Shared/Models/Network/Enums/PacketType.cs` nếu cần thêm enum packet.
- `Monopoly.Shared/Models/Network/Payloads/...` nếu cần thêm DTO riêng.

**Packet đề xuất:**

```json
{ "Type": "USE_CARD", "Payload": { "RoomId": "...", "Username": "...", "CardId": "..." } }
```

**Unity cần chỉnh:**

- `PlayerHandUI` panel/list.
- Button cho từng thẻ trong hand.
- Text/icon trạng thái thẻ đã dùng nếu có.
- Thông báo lỗi khi server từ chối use card.

**Việc làm:**

- Hiện thẻ trong hand thành button có thể click.
- Click thẻ gửi `USE_CARD`.
- Server validate đúng room, đúng player, player có thẻ và nếu cần thì đúng lượt.
- Server apply effect trong `GameEngine` hoặc helper logic riêng.
- Server remove/clear flag thẻ đã dùng.
- Server broadcast `GAME_STATE_UPDATE`.

**Áp dụng trước:**

- `ESCAPE_ISLAND`.
- `FREE_RENT`.
- `FORCE_DOUBLE`.
- `FREE_UPGRADE`.

**Test cần chạy:**

- Có thẻ thì UI hiện button.
- Dùng thẻ thành công và state thay đổi đúng.
- Thẻ biến mất hoặc cập nhật trạng thái sau khi dùng.
- Không dùng được thẻ không sở hữu.
- Không dùng được thẻ sai room/sai user/sai thời điểm.
- 2 client đều thấy state mới sau `GAME_STATE_UPDATE`.

### CARD-02 Card Target Selection

**Mục tiêu:** Các thẻ cần chọn target phải có UI highlight target hợp lệ và server validate lựa chọn.

**File cần sửa:**

- `Monopoly.Server/Handles/GameHandler.cs`
- `Monopoly.Server/GameLogic/GameEngine.cs`
- `Monopoly.Server/Models/State/Room.cs` nếu cần lưu pending choice theo room.
- `Monopoly.Server/Models/State/GameState.cs` nếu cần expose pending choice cho client.
- `Monopoly.Client.Unity/Assets/Scripts/BoardTileInfoUI.cs` hoặc script highlight mới.
- `Monopoly.Client.Unity/Assets/Scripts/PlayerHandUI.cs`
- `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs`
- Shared DTO nếu cần cho `REQUEST_CARD_CHOICE` và `CARD_CHOICE_MADE`.

**Packet đề xuất:**

- Server gửi `REQUEST_CARD_CHOICE`.
- Client gửi `CARD_CHOICE_MADE`.

**Unity cần chỉnh:**

- Highlight overlay cho ô hợp lệ.
- Cursor/selection state cho board tile đang được chọn.
- Nút cancel nếu action optional.
- Text hướng dẫn ngắn trong panel hand/card action.

**Việc làm:**

- Khi dùng card cần target, server tạo pending choice và chỉ gửi request cho player liên quan.
- Client chỉ highlight các ô/target hợp lệ.
- Click target gửi `CARD_CHOICE_MADE`.
- Server validate target hợp lệ trước khi apply effect.
- Clear pending choice sau khi thành công, cancel hoặc hết timeout nếu có.

**Áp dụng:**

- `FLIGHT`.
- `FREE_UPGRADE`.
- `EARTHQUAKE`.
- `POWER_OUTAGE`.
- `MOVE_CHAMPIONSHIP`.

**Test cần chạy:**

- Chỉ người cần chọn target thấy highlight.
- Client khác vẫn xem state bình thường, không bị khóa input sai.
- Click ô hợp lệ gửi target và effect apply đúng.
- Click ô không hợp lệ bị chặn ở client hoặc bị server reject.
- Cancel action optional không làm kẹt turn/state.

### RESUME-01 Reconnect/Resume UI

**Mục tiêu:** UI cho phép người chơi reconnect/login lại và resume trận đang dở. Packet `RESUME_GAME` và response `RESUME_GAME_NONE` đã có dấu vết trong code, task này tập trung hoàn thiện flow UI và test end-to-end.

**File cần sửa:**

- `Monopoly.Client.Unity/Assets/Scripts/NetworkManager.cs`
- `Monopoly.Client.Unity/Assets/Scripts/LobbyManager.cs`
- Auth/login UI script nếu cần thêm nút resume.
- `Monopoly.Server/Handles/GameHandler.cs` nếu cần bổ sung message/state resume.

**Unity cần chỉnh:**

- Thêm nút `Btn_ResumeGame`.
- Thêm text trạng thái resume.
- Đảm bảo scene transition về `GameScene` khi resume thành công.

**Việc làm:**

- Từ UI gọi `RESUME_GAME`.
- Xử lý `RESUME_GAME_NONE` bằng thông báo rõ ràng.
- Nếu có trận đang dở, load lại `GameScene` và bind state mới.
- Đảm bảo user không bị join room cũ hai lần.

**Test cần chạy:**

- Đang chơi thì mất kết nối rồi login lại.
- Bấm resume và vào đúng room/state.
- Không có trận đang dở thì báo rõ, không load sai scene.
- Resume xong có thể roll/end turn/chat bình thường nếu đến lượt.

### BUILD-01 Build Executable Hardening

**Mục tiêu:** Tạo build `.exe` ổn định cho demo nhiều client.

**File cần sửa:**

- `Monopoly.Client.Unity/ProjectSettings/ProjectSettings.asset` nếu cần chỉnh resolution/window mode.
- `Monopoly.Client.Unity/ProjectSettings/EditorBuildSettings.asset` nếu cần chỉnh scene order.

**Unity cần chỉnh:**

- Build Settings đúng scene order:
  - Login/Auth scene nếu có.
  - `LobbyScene`.
  - `GameScene`.
- Player Settings resolution 1920x1080 windowed.
- Kiểm tra server IP/port config cho build.

**Việc làm:**

- Chuẩn hóa scene order trước khi build.
- Chuẩn hóa window mode/resolution để 2 client dễ sắp xếp khi demo.
- Đảm bảo build không tham chiếu asset/editor-only bị thiếu.

**Test cần chạy:**

- Build `.exe` thành công.
- Chạy 2 client build cùng lúc.
- Editor + `.exe` cùng join 1 room.
- Login, ready/start, roll, buy, chat, game over không lỗi build-only.

## 4. Task Ưu Tiên Đề Xuất

1. `CARD-01 Use Card Logic`
2. `CARD-02 Card Target Selection`
3. `UI-03 BoardTileInfoUI prefab/panel`
4. `UI-04 ChanceCardUI prefab`
5. `UI-05 GameOverUI + Leaderboard polish`
6. `RESUME-01 Reconnect/Resume UI`
7. `DEMO-01 Demo hardening`
8. `BUILD-01 Build executable hardening`

## 5. Checklist Test Gameplay Chính

- Login/register thành công với 2 account.
- Tạo phòng, join phòng, ready và start game.
- Server broadcast state đúng cho tất cả client.
- Roll dice chỉ thành công khi đúng lượt.
- Dice visual của tất cả client khớp kết quả server.
- Token di chuyển đúng theo `LastMoveFromPosition -> LastMoveToPosition -> LastFinalPosition`.
- Đi qua Start nếu có rule thu tiền thì tiền cập nhật đúng.
- Mua đất thành công khi đủ tiền và đất chưa có owner.
- Tính rent đúng khi đứng vào đất của người khác.
- Build house/hotel chỉ cho owner và cập nhật marker đúng.
- Bankruptcy cập nhật state player và không phá turn order.
- Game over hiện đúng winner/ranking.
- Reward score và leaderboard Firebase hiện đúng ở cuối trận.
- Chat gửi/nhận được giữa 2 client.
- Popup tile/card/game over không che nút quan trọng và không làm kẹt input.

## 6. Acceptance Criteria Cho Tài Liệu Này

- Có 3 nhóm rõ ràng: tính năng đã có, tính năng cần cải thiện, tính năng cần bổ sung.
- Mỗi task cải thiện/bổ sung có mục tiêu, file cần sửa, Unity cần chỉnh, việc làm và test cần chạy.
- Có thứ tự ưu tiên task.
- Có checklist test gameplay chính.
- Có cảnh báo không đổi `BoardPoint_00..31`, token mapping và server authoritative logic.
- Không sửa code/gameplay trong bước tạo tài liệu.

## 7. Match Timer, Audio, Settings Và Public Network

### TIME-01 Match Duration - Đã triển khai code

- Host chọn thời lượng `10/20/30/60` phút; nếu UI chưa gán dropdown thì mặc định `20` phút.
- Server tự kết thúc khi hết giờ và xếp hạng theo tiền mặt cộng giá trị thanh lý đất/nhà/khách sạn.
- Đồng hồ `MM:SS` được tạo runtime trong GameScene và chuyển đỏ ở 60 giây cuối.

Unity cần chỉnh:

- Tạo `TMP_Dropdown` tên `Dropdown_MatchDuration` trong `Panel_RoomSettings`.
- Kéo dropdown vào field `Dropdown Match Duration` của `LobbyManager`.
- Đồng hồ GameScene không cần kéo thả.

### AUDIO-01 Audio Manager - Đã triển khai code và asset

- `AudioManager` giữ Music/SFX source qua scene và lưu volume/mute bằng `PlayerPrefs`.
- Có API `PlayMusic(AudioClip)` và `PlaySfx(AudioClip)`.
- Đã thêm các clip `build`, `click`, `dice`, `jump` trong `Assets/Resources/Audio`.
- Chưa kiểm tra đầy đủ âm thanh trong một ván multiplayer thực tế.

### SETTINGS-01 Settings Menu - Đã triển khai code

- Nút `SET` và panel settings được tạo runtime.
- Có Master/Music/SFX volume, mute, pause vote và đầu hàng có xác nhận.

### PAUSE-01 Multiplayer Pause Vote - Đã triển khai code

- `REQUEST_PAUSE` tạo yêu cầu; các người chơi thật còn online gửi `PAUSE_VOTE`.
- Khi tất cả đồng ý, server dừng match timer, turn timer, bot và chặn thao tác gameplay.
- `RESUME_GAMEPLAY` cộng bù thời gian đã pause.

### SURRENDER-01 Surrender - Đã triển khai code

- `SURRENDER_GAME` xử lý người chơi theo luồng phá sản, giải phóng tài sản và kiểm tra game over.

### NET-01/NET-02 Internet Public Server - Đã có cấu hình, chưa deploy

- `ServerConnectionConfig` đọc endpoint từ `StreamingAssets/server-config.json`.
- Server hỗ trợ cấu hình port và đã có `INTERNET_DEPLOYMENT_GUIDE.md`.
- Chưa deploy VPS, mở firewall hoặc kiểm thử hai mạng Internet khác nhau.

### Thứ tự tiếp theo

1. Test timer bằng cấu hình dev rút ngắn.
2. Test pause với 2 client và client + bot.
3. Test đầu hàng ở lượt hiện tại và ngoài lượt.
4. Thêm clip và nối event cho audio.
5. Polish settings thành prefab nếu có design.
6. Thực hiện cấu hình Internet cuối cùng.

## 8. Gameplay Và UI Bổ Sung - Đã Merge Code

### GAME-DOUBLE/JAIL - Đã triển khai và có test

- Đổ đôi được thêm lượt; ba lần đôi liên tiếp đưa người chơi vào Đảo Hoang.
- Đảo Hoang cho tối đa ba lượt lắc đôi, sau đó trả phí để rời đảo.
- Logic chuyển lượt dùng chung `StartNextTurnUnsafe`.

### GAME-MONOPOLY - Đã triển khai và có test

- Tăng tiền thuê khi sở hữu trọn nhóm màu.
- Bổ sung điều kiện thắng nhanh theo Resort Monopoly, Line Monopoly và Triple Monopoly.
- World Tour cho người chơi chọn điểm đến; Giải Vô Địch cho chọn thành phố đăng cai.

### UI-MONEY/BUILD - Đã triển khai code

- `MoneyFlowUI` hiển thị biến động tiền và hiệu ứng thanh toán.
- `PropertyBuildMarkerUI` hiển thị marker nhà/khách sạn.
- `PlayerInfoLayerUI` và demo layout đã được điều chỉnh.
- Cần kiểm tra trực quan và multiplayer trong Unity Editor.

## 9. Quy Tắc Cập Nhật Tiến Độ

Đây là file trạng thái chính của project. Sau mỗi thay đổi về tính năng, gameplay, network, UI Unity, scene/prefab, cấu hình build hoặc sửa lỗi:

1. Cập nhật trạng thái và mô tả của task liên quan trong tài liệu này.
2. Ghi rõ file, scene hoặc prefab đã thay đổi.
3. Ghi hành vi mới, lỗi đã sửa và các giới hạn còn lại.
4. Ghi kết quả build/test thực tế; không đánh dấu hoàn thành nếu chưa kiểm tra.
5. Thêm một mục mới ở đầu phần `Nhật ký thay đổi`.

## 10. Nhật Ký Thay Đổi

### 2026-06-25

- Resolve merge `feature/logic_game` vào `main`, giữ các sửa lỗi encoding và xử lý khi không còn người chơi thật.
- Bổ sung gameplay đổ đôi/Đảo Hoang, monopoly rent và điều kiện thắng nhanh.
- Bổ sung `MoneyFlowUI`, marker nhà/khách sạn, audio asset và cấu hình public server.
- Sửa null-safety khi chuyển lượt, chuỗi nội suy Xúc Xắc Ma Thuật và các text gameplay lỗi dấu.
- Server build thành công: `0` lỗi.
- Tám test `GameEngineTests` chạy thành công.
- Unity `2022.3.62f3` import và compile batch mode thành công, không có lỗi C#.
