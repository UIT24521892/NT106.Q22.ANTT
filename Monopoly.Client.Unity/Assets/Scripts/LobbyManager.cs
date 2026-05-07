using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;
using System.Text;
using System.IO;

// ============================================================
//  LobbyManager.cs
//  Quản lý toàn bộ luồng giao diện Sảnh chờ (Lobby)
//  Bao gồm: Main Menu → Room Settings → Waiting Room
//  Kết nối TCP qua NetworkManager.ServerStream
// ============================================================

public class LobbyManager : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────
    // SECTION 1: REFERENCES ĐẾN CÁC PANEL CHÍNH
    // ──────────────────────────────────────────────────────────

    [Header("=== CÁC PANEL CHÍNH ===")]
    [SerializeField] private GameObject panelMainMenu;      // Panel 1: Menu chính
    [SerializeField] private GameObject panelRoomSettings;  // Panel 2: Tạo phòng
    [SerializeField] private GameObject panelWaitingRoom;   // Panel 3: Phòng chờ
    [SerializeField] private GameObject panelRoomList;      // Panel 4: Danh sách phòng

    // ──────────────────────────────────────────────────────────
    // SECTION 2: UI COMPONENTS - PANEL MAIN MENU
    // ──────────────────────────────────────────────────────────

    [Header("=== MAIN MENU UI ===")]
    [SerializeField] private TMP_Text txtPlayerUsername;    // Tên người chơi
    [SerializeField] private TMP_Text txtPlayerBalance;     // Số dư tiền tệ
    [SerializeField] private Image imgPlayerAvatar;         // Ảnh đại diện

    // ──────────────────────────────────────────────────────────
    // SECTION 3: UI COMPONENTS - PANEL ROOM SETTINGS
    // ──────────────────────────────────────────────────────────

    [Header("=== ROOM SETTINGS UI ===")]
    [SerializeField] private Slider sliderMaxPlayers;       // Slider chọn số người (2-4)
    [SerializeField] private TMP_Text txtMaxPlayersValue;   // Hiển thị giá trị slider người chơi
    [SerializeField] private Slider sliderBotCount;         // Slider chọn số bot
    [SerializeField] private TMP_Text txtBotCountValue;     // Hiển thị giá trị slider bot
    [SerializeField] private TMP_Dropdown dropdownMap;      // Dropdown chọn bản đồ
    [SerializeField] private TMP_Text txtRoomSettingsError; // Text hiển thị lỗi validation

    // ──────────────────────────────────────────────────────────
    // SECTION 4: UI COMPONENTS - PANEL WAITING ROOM
    // ──────────────────────────────────────────────────────────

    [Header("=== WAITING ROOM UI ===")]
    [SerializeField] private Transform playerListContainer; // Parent chứa các slot người chơi
    [SerializeField] private GameObject playerSlotPrefab;   // Prefab 1 slot người chơi trong danh sách
    [SerializeField] private TMP_Text txtRoomId;            // Hiển thị mã phòng
    [SerializeField] private TMP_Text txtRoomMap;           // Hiển thị map đang chọn
    [SerializeField] private Button btnReady;               // Nút "Sẵn sàng" (client thường)
    [SerializeField] private Button btnStart;               // Nút "Bắt đầu" (chỉ host)
    [SerializeField] private TMP_Text txtBtnReady;          // Text trên nút Ready (để đổi màu/text)

//  ──────────────────────────────────────────────────────────
    // SECTION 4.5: UI COMPONENTS - PANEL ROOM LIST
    // ──────────────────────────────────────────────────────────

    [Header("=== ROOM LIST UI ===")]
    [SerializeField] private Transform roomListContainer; // Kéo object 'Content' nằm trong Scroll View vào đây
    [SerializeField] private GameObject roomSlotPrefab;   // Prefab 1 dòng thông tin phòng
    [SerializeField] private TMP_Text txtEmptyRoom;       // Text "Không có phòng nào đang chờ" (Txt_Empty)

    // ──────────────────────────────────────────────────────────
    // SECTION 5: TRẠNG THÁI NỘI BỘ (PRIVATE STATE)
    // ──────────────────────────────────────────────────────────

    private bool isReady = false;           // Trạng thái sẵn sàng của người chơi này
    private bool isHost = false;            // Người chơi này có phải chủ phòng không?
    private string currentRoomId = "";      // Mã phòng hiện tại đang tham gia

    // ──────────────────────────────────────────────────────────
    // SECTION 6: KHỞI TẠO
    // ──────────────────────────────────────────────────────────

    private void Start()
    {
        // Khởi tạo slider với giá trị hợp lệ
        InitSliders();

        // Khởi tạo dropdown danh sách map
        InitMapDropdown();

        // Hiển thị thông tin người chơi từ session đăng nhập
        LoadPlayerInfo();

        // Bắt đầu ở Panel Main Menu
        ShowPanel(panelMainMenu);
    }

    /// <summary>
    /// Thiết lập các Slider với giá trị min/max ban đầu
    /// </summary>
    private void InitSliders()
    {
        if (sliderMaxPlayers != null)
        {
            sliderMaxPlayers.minValue = 2;
            sliderMaxPlayers.maxValue = 4;
            sliderMaxPlayers.wholeNumbers = true;
            sliderMaxPlayers.value = 4; // Mặc định 4 người
            OnMaxPlayersSliderChanged(sliderMaxPlayers.value);

            // Đăng ký listener cập nhật text khi kéo slider
            sliderMaxPlayers.onValueChanged.AddListener(OnMaxPlayersSliderChanged);
        }

        if (sliderBotCount != null)
        {
            sliderBotCount.minValue = 0;
            sliderBotCount.maxValue = 3;
            sliderBotCount.wholeNumbers = true;
            sliderBotCount.value = 0; // Mặc định không có bot
            OnBotCountSliderChanged(sliderBotCount.value);

            sliderBotCount.onValueChanged.AddListener(OnBotCountSliderChanged);
        }
    }

    /// <summary>
    /// Khởi tạo danh sách các Map có thể chọn trong Dropdown
    /// </summary>
    private void InitMapDropdown()
    {
        if (dropdownMap == null) return;

        dropdownMap.ClearOptions();
        var mapOptions = new List<string>
        {
            "Thế Giới (Classic)",
            "Việt Nam",
            "Châu Á",
            "Châu Âu"
        };
        dropdownMap.AddOptions(mapOptions);
    }

    /// <summary>
    /// Tải thông tin người chơi từ PlayerSession (singleton giả định đã lưu sau đăng nhập)
    /// </summary>
    private void LoadPlayerInfo()
    {
        // Giả định PlayerSession.Instance đã lưu dữ liệu sau khi AuthManager đăng nhập thành công
        // Bạn cần tạo class PlayerSession hoặc thay thế bằng cách lấy data tương ứng.
        if (txtPlayerUsername != null)
            txtPlayerUsername.text = PlayerSession.Instance?.Username ?? "Player";

        if (txtPlayerBalance != null)
            txtPlayerBalance.text = FormatCurrency(PlayerSession.Instance?.Balance ?? 2000000);
    }

    // ──────────────────────────────────────────────────────────
    // SECTION 7: ĐIỀU HƯỚNG PANEL
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Hàm dùng chung: ẩn tất cả panel rồi bật đúng panel cần hiện
    /// </summary>
    /// <param name="targetPanel">Panel GameObject cần hiển thị</param>
    private void ShowPanel(GameObject targetPanel)
    {
        // Tắt toàn bộ panel trước
        panelMainMenu.SetActive(false);
        panelRoomSettings.SetActive(false);
        panelWaitingRoom.SetActive(false);
        panelRoomList.SetActive(false); // Thêm dòng này

        // Bật đúng panel mục tiêu
        if (targetPanel != null)
            targetPanel.SetActive(true);
    }

    // ──────────────────────────────────────────────────────────
    // SECTION 8: HANDLER SỰ KIỆN SLIDER
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Callback khi slider "Số người chơi" thay đổi — cập nhật text hiển thị
    /// </summary>
    public void OnMaxPlayersSliderChanged(float value)
    {
        if (txtMaxPlayersValue != null)
            txtMaxPlayersValue.text = $"{(int)value} người";

        // Giới hạn bot không vượt quá (maxPlayers - 1) để luôn có ít nhất 1 người thật
        if (sliderBotCount != null)
        {
            sliderBotCount.maxValue = value - 1;
            if (sliderBotCount.value > sliderBotCount.maxValue)
                sliderBotCount.value = sliderBotCount.maxValue;
        }
    }

    /// <summary>
    /// Callback khi slider "Số Bot" thay đổi — cập nhật text hiển thị
    /// </summary>
    public void OnBotCountSliderChanged(float value)
    {
        if (txtBotCountValue != null)
        {
            int botCount = (int)value;
            txtBotCountValue.text = botCount == 0 ? "Không có Bot" : $"{botCount} Bot";
        }
    }

    // ──────────────────────────────────────────────────────────
    // SECTION 9: BUTTON HANDLERS - MAIN MENU
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// [Gán vào nút "TẠO PHÒNG"]
    /// Chuyển sang Panel Room Settings để người chơi cấu hình phòng
    /// </summary>
    public void OnBtnCreateRoomClicked()
    {
        // Reset lỗi cũ nếu có
        if (txtRoomSettingsError != null)
            txtRoomSettingsError.text = "";

        ShowPanel(panelRoomSettings);
    }

    /// <summary>
    /// [Gán vào nút "TÌM PHÒNG"]
    /// Gửi yêu cầu lên Server để lấy danh sách phòng đang chờ
    /// </summary>
    public void OnBtnJoinRoomClicked()
    {

        ShowPanel(panelRoomList);
        
        if (txtEmptyRoom != null) 
        {
            txtEmptyRoom.gameObject.SetActive(true);
            txtEmptyRoom.text = "Đang tải danh sách phòng...";
        }


        // ── Đóng gói gói tin JSON ──────────────────────────────
        // Type: "GET_ROOM_LIST" — Server sẽ trả về danh sách phòng còn chỗ trống
        var packet = new
        {
            Type = "GET_ROOM_LIST",
            Payload = new { } // Không cần payload, Server tự lọc phòng available
        };

        SendPacketToServer(packet);

        // TODO: Lắng nghe phản hồi từ Server ở NetworkManager
        //       Khi nhận được "ROOM_LIST_RESPONSE", hiển thị Panel chọn phòng
        Debug.Log("[LobbyManager] Đã gửi yêu cầu lấy danh sách phòng.");
    }

    /// <summary>
    /// [Gán vào nút "ĐĂNG XUẤT"]
    /// Ngắt kết nối TCP và trở về màn hình đăng nhập
    /// </summary>
    public void OnBtnLogoutClicked()
    {
        // ── Gửi thông báo đăng xuất cho Server ────────────────
        var packet = new
        {
            Type = "LOGOUT",
            Payload = new
            {
                Username = PlayerSession.Instance?.Username
            }
        };

        SendPacketToServer(packet);

        // Ngắt kết nối
        NetworkManager.Instance?.Disconnect();

        // Tải lại Scene Login (đảm bảo "LoginScene" đúng tên trong Build Settings)
        UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
    }

    // ──────────────────────────────────────────────────────────
    // SECTION 10: BUTTON HANDLERS - ROOM SETTINGS
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// [Gán vào nút "XÁC NHẬN TẠO"]
    /// Validate dữ liệu → Đóng gói JSON → Gửi yêu cầu tạo phòng lên Server
    /// </summary>
    public void OnBtnConfirmCreateRoomClicked()
    {
        // ── Bước 1: Validate dữ liệu người dùng nhập ──────────
        int maxPlayers = (int)sliderMaxPlayers.value;
        int botCount = (int)sliderBotCount.value;
        string selectedMap = dropdownMap.options[dropdownMap.value].text;

        // Kiểm tra: số bot + 1 người thật không vượt quá maxPlayers
        if (botCount >= maxPlayers)
        {
            ShowRoomSettingsError("Phải có ít nhất 1 người chơi thật trong phòng!");
            return;
        }

        // ── Bước 2: Đóng gói gói tin JSON ────────────────────
        // Type: "CREATE_ROOM" — Server sẽ tạo phòng, gán RoomID và trả về
        var packet = new
        {
            Type = "CREATE_ROOM",
            Payload = new
            {
                HostUsername = PlayerSession.Instance?.Username,
                MaxPlayers = maxPlayers,
                BotCount = botCount,
                MapName = selectedMap
            }
        };

        SendPacketToServer(packet);

        // ── Bước 3: Đánh dấu người này là chủ phòng ──────────
        // (Trạng thái chính thức sẽ được Server xác nhận qua phản hồi)
        isHost = true;

        // ── Bước 4: Chuẩn bị Waiting Room cho Host ────────────
        // Hiển thị nút "Bắt đầu" thay vì "Sẵn sàng"
        SetupWaitingRoomForHost();

        // Chuyển sang Waiting Room (Server sẽ gửi lại thông tin phòng đầy đủ)
        ShowPanel(panelWaitingRoom);

        Debug.Log($"[LobbyManager] Đã gửi yêu cầu tạo phòng: MaxPlayers={maxPlayers}, Bot={botCount}, Map={selectedMap}");
    }

    /// <summary>
    /// [Gán vào nút "HỦY" trong Room Settings]
    /// Quay lại Main Menu, không gửi gì lên Server
    /// </summary>
    public void OnBtnCancelCreateRoomClicked()
    {
        ShowPanel(panelMainMenu);
    }

    // ──────────────────────────────────────────────────────────
    // SECTION 11: BUTTON HANDLERS - WAITING ROOM
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// [Gán vào nút "SẴN SÀNG"] — Chỉ hiển thị cho client thường (không phải host)
    /// Toggle trạng thái Ready/Not Ready và gửi lên Server
    /// </summary>
    public void OnBtnReadyClicked()
    {
        // Toggle trạng thái sẵn sàng
        isReady = !isReady;

        // ── Cập nhật giao diện nút ────────────────────────────
        if (txtBtnReady != null)
        {
            txtBtnReady.text = isReady ? "✓ Đã Sẵn Sàng" : "Sẵn Sàng";
        }

        // Đổi màu nút để phản hồi trực quan
        if (btnReady != null)
        {
            var colors = btnReady.colors;
            colors.normalColor = isReady
                ? new Color(0.2f, 0.8f, 0.3f, 1f)  // Xanh lá khi sẵn sàng
                : new Color(0.9f, 0.9f, 0.9f, 1f);  // Xám khi chưa sẵn sàng
            btnReady.colors = colors;
        }

        // ── Đóng gói và gửi gói tin JSON ──────────────────────
        // Type: "PLAYER_READY" — Server cập nhật trạng thái và broadcast cho phòng
        var packet = new
        {
            Type = "PLAYER_READY",
            Payload = new
            {
                RoomId = currentRoomId,
                Username = PlayerSession.Instance?.Username,
                IsReady = isReady
            }
        };

        SendPacketToServer(packet);

        Debug.Log($"[LobbyManager] Người chơi '{PlayerSession.Instance?.Username}' -> IsReady = {isReady}");
    }

    /// <summary>
    /// [Gán vào nút "BẮT ĐẦU"] — Chỉ hiển thị cho Host
    /// Gửi yêu cầu bắt đầu game, Server kiểm tra tất cả đã ready chưa
    /// </summary>
    public void OnBtnStartGameClicked()
    {
        // ── Đóng gói gói tin JSON ──────────────────────────────
        // Type: "START_GAME" — Server kiểm tra điều kiện (tất cả ready, đủ người),
        //                       nếu hợp lệ sẽ broadcast "GAME_STARTING" cho tất cả client
        var packet = new
        {
            Type = "START_GAME",
            Payload = new
            {
                RoomId = currentRoomId,
                HostUsername = PlayerSession.Instance?.Username
            }
        };

        SendPacketToServer(packet);

        Debug.Log($"[LobbyManager] Host '{PlayerSession.Instance?.Username}' yêu cầu bắt đầu game ở phòng {currentRoomId}");

        // TODO: Lắng nghe phản hồi "GAME_STARTING" từ Server
        //       Khi nhận được → LoadScene("GameScene")
    }

    /// <summary>
    /// [Gán vào nút "THOÁT PHÒNG"] trong Waiting Room
    /// Gửi thông báo rời phòng, quay về Main Menu
    /// </summary>
    public void OnBtnLeaveRoomClicked()
    {
        // ── Đóng gói gói tin JSON ──────────────────────────────
        // Type: "LEAVE_ROOM" — Server xóa người chơi khỏi phòng,
        //                       nếu là Host thì giải tán phòng hoặc chuyển quyền
        var packet = new
        {
            Type = "LEAVE_ROOM",
            Payload = new
            {
                RoomId = currentRoomId,
                Username = PlayerSession.Instance?.Username,
                IsHost = isHost
            }
        };

        SendPacketToServer(packet);

        // Reset trạng thái local
        ResetLobbyState();

        // Quay về Main Menu
        ShowPanel(panelMainMenu);

        Debug.Log($"[LobbyManager] Đã rời phòng {currentRoomId}");
    }

    // ──────────────────────────────────────────────────────────
    // SECTION 12: CÁC HÀM CẬP NHẬT UI TỪ DỮ LIỆU SERVER
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Được gọi bởi NetworkManager khi nhận được phản hồi tạo phòng thành công.
    /// Hiển thị thông tin phòng vừa tạo lên Waiting Room.
    /// </summary>
    /// <param name="roomId">Mã phòng do Server cấp</param>
    /// <param name="mapName">Tên map đã chọn</param>
    public void OnRoomCreatedSuccess(string roomId, string mapName)
    {
        currentRoomId = roomId;

        if (txtRoomId != null)
            txtRoomId.text = $"Mã phòng: #{roomId}";

        if (txtRoomMap != null)
            txtRoomMap.text = $"Map: {mapName}";

        Debug.Log($"[LobbyManager] Phòng được tạo thành công! RoomID = {roomId}");
    }

    /// <summary>
    /// Được gọi bởi NetworkManager khi nhận broadcast cập nhật danh sách người chơi.
    /// Xây dựng lại toàn bộ danh sách UI trong Waiting Room.
    /// </summary>
    /// <param name="players">Danh sách thông tin người chơi trong phòng</param>
    public void RefreshPlayerList(List<PlayerSlotData> players)
    {
        if (playerListContainer == null || playerSlotPrefab == null) return;

        // Xóa toàn bộ slot cũ trước khi vẽ lại
        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        // Tạo slot UI mới cho mỗi người chơi
        foreach (var player in players)
        {
            GameObject slot = Instantiate(playerSlotPrefab, playerListContainer);
            var slotUI = slot.GetComponent<PlayerSlotUI>();

            if (slotUI != null)
            {
                slotUI.Setup(
                    username: player.Username,
                    isReady: player.IsReady,
                    isHost: player.IsHost,
                    isBot: player.IsBot
                );
            }
        }
    }

    // ──────────────────────────────────────────────────────────
    // SECTION 13: HELPER FUNCTIONS
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// Hàm dùng chung để đóng gói object thành JSON + "<EOF>" rồi gửi qua NetworkStream.
    /// Tất cả các gói tin gửi đi đều đi qua hàm này để đảm bảo nhất quán.
    /// </summary>
    /// <param name="packetObject">Object bất kỳ sẽ được serialize thành JSON</param>
    private void SendPacketToServer(object packetObject)
    {
        // ✅ Kiểm tra dùng static field trực tiếp
        if (NetworkManager.ServerStream == null)
        {
            Debug.LogWarning("[LobbyManager] ServerStream là null!");
            return;
        }

        try
        {
            string json = JsonConvert.SerializeObject(packetObject);
            string message = json + "<EOF>";
            byte[] data = Encoding.UTF8.GetBytes(message);

            // ✅ Gọi trực tiếp không qua Instance
            NetworkManager.ServerStream.Write(data, 0, data.Length);
            NetworkManager.ServerStream.Flush();

            Debug.Log($"[LobbyManager] Đã gửi: {json}");
        }
        catch (IOException ex)
        {
            Debug.LogError($"[LobbyManager] Lỗi gửi: {ex.Message}");
        }
    }

// ──────────────────────────────────────────────────────────
    // SECTION 14: BUTTON HANDLERS - ROOM LIST
    // ──────────────────────────────────────────────────────────

    /// <summary>
    /// [Gán vào nút "Trở về" (Btn_Return) trong Panel Room List]
    /// </summary>
    public void OnBtnReturnFromRoomListClicked()
    {
        ShowPanel(panelMainMenu);
    }

    /// <summary>
    /// [Gán vào nút "Làm mới" (Btn_Refresh) trong Panel Room List]
    /// </summary>
    public void OnBtnRefreshRoomListClicked()
    {
        if (txtEmptyRoom != null) 
        {
            txtEmptyRoom.gameObject.SetActive(true);
            txtEmptyRoom.text = "Đang làm mới...";
        }

        var packet = new
        {
            Type = "GET_ROOM_LIST",
            Payload = new { }
        };
        SendPacketToServer(packet);
    }

    /// <summary>
    /// [Gán vào nút "Tạo phòng" (Btn_Create) trong Panel Room List]
    /// </summary>
    public void OnBtnCreateFromRoomListClicked()
    {
        OnBtnCreateRoomClicked(); // Tái sử dụng hàm đã viết ở Main Menu
    }


    /// <summary>
    /// Cấu hình Waiting Room cho vai trò Host:
    /// Ẩn nút "Sẵn sàng", hiện nút "Bắt đầu"
    /// </summary>
    private void SetupWaitingRoomForHost()
    {
        if (btnReady != null) btnReady.gameObject.SetActive(false);
        if (btnStart != null) btnStart.gameObject.SetActive(true);
    }

    /// <summary>
    /// Cấu hình Waiting Room cho vai trò Client thường:
    /// Hiện nút "Sẵn sàng", ẩn nút "Bắt đầu"
    /// </summary>
    private void SetupWaitingRoomForClient()
    {
        if (btnReady != null) btnReady.gameObject.SetActive(true);
        if (btnStart != null) btnStart.gameObject.SetActive(false);
        isReady = false; // Reset trạng thái ready khi vào phòng
    }

    /// <summary>
    /// Hiển thị thông báo lỗi trong Panel Room Settings
    /// </summary>
    private void ShowRoomSettingsError(string message)
    {
        if (txtRoomSettingsError != null)
        {
            txtRoomSettingsError.text = message;
            txtRoomSettingsError.color = Color.red;
        }
    }

    /// <summary>
    /// Reset tất cả trạng thái nội bộ về giá trị mặc định khi rời phòng
    /// </summary>
    private void ResetLobbyState()
    {
        isReady = false;
        isHost = false;
        currentRoomId = "";

        // Reset giao diện nút Ready về trạng thái ban đầu
        if (txtBtnReady != null) txtBtnReady.text = "Sẵn Sàng";
        if (btnReady != null)
        {
            var colors = btnReady.colors;
            colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            btnReady.colors = colors;
        }
    }

    /// <summary>
    /// Format số tiền thành chuỗi dễ đọc (ví dụ: 2.000.000 đ)
    /// </summary>
    private string FormatCurrency(long amount)
    {
        return $"{amount:N0} đ".Replace(",", ".");
    }
}


// ============================================================
//  DATA CLASSES - Dùng để deserialize JSON từ Server
// ============================================================

/// <summary>
/// Dữ liệu 1 slot người chơi nhận từ Server (trong gói "ROOM_UPDATE")
/// </summary>
[System.Serializable]
public class PlayerSlotData
{
    public string Username;
    public bool IsReady;
    public bool IsHost;
    public bool IsBot;
    public string AvatarUrl; // URL ảnh avatar (nếu có)
}

// ============================================================
//  PLAYER SESSION SINGLETON (STUB)
//  Lớp này lưu thông tin phiên đăng nhập sau khi AuthManager xác thực xong.
//  Bạn có thể đã có class này rồi — nếu có hãy bỏ phần này đi.
// ============================================================

public class PlayerSession
{
    public static PlayerSession Instance { get; private set; }

    public string Username { get; private set; }
    public long Balance { get; private set; }
    public string AvatarUrl { get; private set; }

    /// <summary>
    /// Được gọi bởi AuthManager sau khi đăng nhập thành công
    /// </summary>
    public static void Initialize(string username, long balance, string avatarUrl = "")
    {
        Instance = new PlayerSession
        {
            Username = username,
            Balance = balance,
            AvatarUrl = avatarUrl
        };
    }

    /// <summary>
    /// Xóa session khi đăng xuất
    /// </summary>
    public static void Clear()
    {
        Instance = null;
    }
}