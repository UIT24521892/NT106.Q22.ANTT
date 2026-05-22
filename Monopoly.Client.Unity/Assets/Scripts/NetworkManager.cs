using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

// ============================================================
// NetworkManager.cs
// Quản lý kết nối TCP giữa Unity Client và Server.
// Lưu ý:
// - AuthManager hiện tại vẫn tự đọc response Login/Register dạng SUCCESS|uid|token.
// - NetworkManager chỉ bắt đầu listen sau khi vào LobbyScene,
//   thông qua LobbyManager.Start() gọi StartListeningToServer().
// ============================================================

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private GameObject splashPanel;
    [SerializeField] private GameObject authPanel;

    public static TcpClient ClientSocket;
    public static NetworkStream ServerStream;

    private bool isListening = false;
    private readonly byte[] buffer = new byte[4096];
    private string pendingData = "";
    private TextMeshProUGUI gameStateOverlayText;
    private TextMeshProUGUI gameActionLogText;
    private TextMeshProUGUI gameErrorText;
    private Button rollButton;
    private Button buyButton;
    private Button endTurnButton;
    private string lastGameActionError = "";
    private string lastClientActionStatus = "";
    private long serverClockOffsetTicks = 0;
    private const int MaxChatMessages = 30;
    private readonly List<ChatMessageData> gameChatMessages = new List<ChatMessageData>();

    public event Action<ChatMessageData> GameChatMessageReceived;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        if (authPanel != null)
            authPanel.SetActive(false);

        if (splashPanel != null)
            splashPanel.SetActive(true);

        if (txtStatus != null)
            txtStatus.text = "Đang kết nối tới Máy chủ TCP...";

        await ConnectToServerAsync();
    }

    private void Update()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GameScene")
        {
            return;
        }

        UpdateGameStateOverlayText();
        UpdateGameplayButtons();

        if (Input.GetKeyDown(KeyCode.R))
        {
            SendDiceRollRequest();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            SendEndTurnRequest();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            SendBuyPropertyRequest();
        }
    }

    private void OnGUI()
    {
        // Runtime IMGUI overlay is disabled. GameScene UI is bound through GameSceneUIBinder.
    }

    public void RegisterGameplayUi(
        Button roll,
        Button buy,
        Button endTurn,
        TextMeshProUGUI stateText,
        TextMeshProUGUI actionLogText,
        TextMeshProUGUI errorText)
    {
        rollButton = roll;
        buyButton = buy;
        endTurnButton = endTurn;
        gameStateOverlayText = stateText;
        gameActionLogText = actionLogText;
        gameErrorText = errorText;

        ConfigureGameplayText(gameStateOverlayText, 22f);
        ConfigureGameplayText(gameActionLogText, 18f);
        ConfigureGameplayText(gameErrorText, 18f);

        BindButton(rollButton, SendDiceRollRequest);
        BindButton(buyButton, SendBuyPropertyRequest);
        BindButton(endTurnButton, SendEndTurnRequest);

        UpdateGameStateOverlayText();
        UpdateGameplayButtons();
    }

    public void UnregisterGameplayUi()
    {
        rollButton = null;
        buyButton = null;
        endTurnButton = null;
        gameStateOverlayText = null;
        gameActionLogText = null;
        gameErrorText = null;
    }

    public IReadOnlyList<ChatMessageData> GetGameChatMessages()
    {
        return gameChatMessages.AsReadOnly();
    }

    private void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void ConfigureGameplayText(TextMeshProUGUI text, float fontSize)
    {
        if (text == null)
            return;

        text.fontSize = fontSize;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private void UpdateGameStateOverlayText()
    {
        if (gameStateOverlayText == null && gameActionLogText == null && gameErrorText == null)
            return;

        GameStateData state = GameSession.CurrentState;

        if (state == null)
        {
            if (gameStateOverlayText != null)
                gameStateOverlayText.text = "Dang cho trang thai game...";
            if (gameActionLogText != null)
                gameActionLogText.text = "";
            if (gameErrorText != null)
                gameErrorText.text = "";
            return;
        }

        if (gameStateOverlayText != null)
            gameStateOverlayText.text = BuildGameStateOverlayText(state);

        if (gameActionLogText != null)
            gameActionLogText.text = BuildActionLogText(state);

        if (gameErrorText != null)
            gameErrorText.text = lastGameActionError;
    }

    private string BuildGameStateOverlayText(GameStateData state)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine($"Room {state.RoomId} | Turn {state.TurnNumber} | {GetRemainingTurnSeconds(state)}s");

        if (state.IsFinished)
        {
            builder.AppendLine($"Game Over | Winner: {ShortName(state.WinnerUsername)}");
        }
        else
        {
            builder.AppendLine($"Turn: {ShortName(state.CurrentTurnUsername)} | {(state.HasRolledThisTurn ? "Rolled" : "Need roll")}");
        }

        if (state.LastDiceTotal > 0)
        {
            builder.AppendLine($"Dice: {state.LastDice1} + {state.LastDice2} = {state.LastDiceTotal}");
        }

        if (state.Players != null)
        {
            foreach (GamePlayerStateData player in state.Players)
            {
                string status = player.IsBankrupt ? "BANKRUPT" : (player.IsConnected ? "ACTIVE" : "DISCONNECTED");
                builder.AppendLine($"{ShortName(player.Username)} P{player.Position} ${player.Money:N0} {status}");
            }
        }

        builder.Append(GetOwnedPropertiesLine(state));

        return builder.ToString();
    }

    private string BuildActionLogText(GameStateData state)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Log:");

        if (!string.IsNullOrWhiteSpace(state.LastActionMessage))
        {
            builder.AppendLine($"Now: {CompactLine(state.LastActionMessage, 56)}");
        }
        else if (!string.IsNullOrWhiteSpace(lastClientActionStatus))
        {
            builder.AppendLine(lastClientActionStatus);
        }

        if (state.ActionLog == null || state.ActionLog.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(state.LastActionMessage) &&
                string.IsNullOrWhiteSpace(lastClientActionStatus))
            {
                builder.AppendLine("No actions.");
            }

            return builder.ToString();
        }

        int startIndex = Math.Max(0, state.ActionLog.Count - 2);

        for (int i = startIndex; i < state.ActionLog.Count; i++)
        {
            builder.AppendLine(CompactLine(state.ActionLog[i], 56));
        }

        return builder.ToString();
    }

    private string GetOwnedPropertiesLine(GameStateData state)
    {
        if (state.Properties == null || state.Players == null)
            return "Owned: None";

        StringBuilder builder = new StringBuilder("Owned: ");
        int count = 0;

        foreach (KeyValuePair<int, GamePropertyStateData> propertyPair in state.Properties)
        {
            GamePropertyStateData property = propertyPair.Value;

            if (property == null || property.OwnerPlayerIndex < 0)
                continue;

            string ownerName = GetPlayerNameByIndex(state, property.OwnerPlayerIndex);
            builder.Append($"{property.PositionIndex}->{ShortName(ownerName)} ");
            count++;

            if (count >= 3)
                break;
        }

        if (count == 0)
            builder.Append("None");

        return builder.ToString();
    }

    private string GetPlayerNameByIndex(GameStateData state, int playerIndex)
    {
        if (state.Players == null)
            return $"P{playerIndex}";

        foreach (GamePlayerStateData player in state.Players)
        {
            if (player.PlayerIndex == playerIndex)
                return player.Username;
        }

        return $"P{playerIndex}";
    }

    private string ShortName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return "Player";

        int atIndex = username.IndexOf("@", StringComparison.Ordinal);

        if (atIndex > 0)
            username = username.Substring(0, atIndex);

        if (username.Length > 12)
            return username.Substring(0, 12);

        return username;
    }

    private string CompactLine(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        string compact = text.Replace("\r", " ").Replace("\n", " ").Trim();

        while (compact.Contains("  "))
            compact = compact.Replace("  ", " ");

        if (compact.Length <= maxLength)
            return compact;

        return compact.Substring(0, maxLength) + "...";
    }

    private void UpdateGameplayButtons()
    {
        if (rollButton == null || buyButton == null || endTurnButton == null)
            return;

        GameStateData state = GameSession.CurrentState;
        GamePlayerStateData localPlayer = GetLocalPlayer(state);
        bool isMyTurn = state != null &&
            localPlayer != null &&
            !state.IsFinished &&
            localPlayer.IsConnected &&
            !localPlayer.IsBankrupt &&
            localPlayer.PlayerIndex == state.CurrentTurnPlayerIndex;

        rollButton.interactable = isMyTurn && !state.HasRolledThisTurn;
        buyButton.interactable = isMyTurn && state.HasRolledThisTurn && CanLocalPlayerBuyCurrentProperty(state, localPlayer);
        endTurnButton.interactable = isMyTurn && state.HasRolledThisTurn;
    }

    private GamePlayerStateData GetLocalPlayer(GameStateData state)
    {
        if (state == null || state.Players == null)
            return null;

        string username = PlayerSession.Instance?.Username ?? "";

        foreach (GamePlayerStateData player in state.Players)
        {
            if (player.Username == username)
                return player;
        }

        return null;
    }

    private bool CanLocalPlayerBuyCurrentProperty(GameStateData state, GamePlayerStateData localPlayer)
    {
        if (state.Properties == null || localPlayer == null)
            return false;

        if (!state.Properties.TryGetValue(localPlayer.Position, out GamePropertyStateData property))
            return false;

        if (property == null || property.OwnerPlayerIndex >= 0 || property.BuyPrice <= 0)
            return false;

        if (property.Type != "City" && property.Type != "Resort")
            return false;

        return localPlayer.Money >= property.BuyPrice;
    }

    private int GetRemainingTurnSeconds(GameStateData state)
    {
        if (state == null || state.IsFinished || state.TurnEndsAtUtcTicks <= 0)
            return 0;

        long estimatedServerNowTicks = DateTime.UtcNow.Ticks + serverClockOffsetTicks;
        long remainingTicks = state.TurnEndsAtUtcTicks - estimatedServerNowTicks;

        if (remainingTicks <= 0)
            return 0;

        return Math.Max(0, (int)Math.Ceiling(TimeSpan.FromTicks(remainingTicks).TotalSeconds));
    }

    private void SyncServerClock(GameStateData state)
    {
        if (state == null || state.ServerUtcTicks <= 0)
            return;

        serverClockOffsetTicks = state.ServerUtcTicks - DateTime.UtcNow.Ticks;
    }

    private async Task ConnectToServerAsync()
    {
        try
        {
            ClientSocket = new TcpClient();

            // Test cùng máy thì dùng 127.0.0.1.
            // Test LAN thì đổi thành IP máy chạy server, ví dụ: 192.168.1.10
            await ClientSocket.ConnectAsync("127.0.0.1", 8080);

            ServerStream = ClientSocket.GetStream();

            if (txtStatus != null)
            {
                txtStatus.color = Color.green;
                txtStatus.text = "Kết nối thành công!";
            }

            await Task.Delay(1000);

            if (splashPanel != null)
                splashPanel.SetActive(false);

            if (authPanel != null)
                authPanel.SetActive(true);

            Debug.Log("[NetworkManager] Kết nối TCP thành công.");

            // KHÔNG gọi StartListeningToServer() ở đây.
            // Vì AuthManager hiện tại vẫn tự đọc response login/register.
            // LobbyManager.Start() sẽ gọi StartListeningToServer() sau khi vào LobbyScene.
        }
        catch (Exception ex)
        {
            if (txtStatus != null)
            {
                txtStatus.color = Color.red;
                txtStatus.text = "Lỗi: Không tìm thấy Máy chủ!";
            }

            Debug.LogError($"[NetworkManager] TCP Error: {ex.Message}");
        }
    }

    // ============================================================
    // GỬI PACKET DÙNG CHUNG
    // Có thể dùng dần để thay thế SendPacketToServer trong LobbyManager.
    // ============================================================
    public async void SendPacket(object packetObject)
    {
        if (ServerStream == null)
        {
            Debug.LogWarning("[NetworkManager] ServerStream null, không thể gửi packet.");
            return;
        }

        try
        {
            string json = JsonConvert.SerializeObject(packetObject);
            string message = json + "<EOF>";
            byte[] data = Encoding.UTF8.GetBytes(message);

            await ServerStream.WriteAsync(data, 0, data.Length);
            await ServerStream.FlushAsync();

            Debug.Log($"[NetworkManager] Đã gửi: {json}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkManager] Lỗi gửi packet: {ex.Message}");
        }
    }

    // ============================================================
    // VÒNG LẶP LẮNG NGHE SERVER
    // Gọi từ LobbyManager.Start()
    // ============================================================
    private void SendDiceRollRequest()
    {
        var packet = new
        {
            Type = "DiceRoll",
            Payload = new
            {
                RoomId = GameSession.RoomId,
                Username = PlayerSession.Instance?.Username
            }
        };

        SendPacket(packet);
        lastClientActionStatus = "Sent: Roll";
        UpdateGameStateOverlayText();
        Debug.Log("[NetworkManager] Đã gửi yêu cầu DiceRoll bằng phím R.");
    }

    private void SendEndTurnRequest()
    {
        var packet = new
        {
            Type = "END_TURN",
            Payload = new
            {
                RoomId = GameSession.RoomId,
                Username = PlayerSession.Instance?.Username
            }
        };

        SendPacket(packet);
        lastClientActionStatus = "Sent: End turn";
        UpdateGameStateOverlayText();
        Debug.Log("[NetworkManager] Đã gửi yêu cầu END_TURN bằng phím E.");
    }

    private void SendBuyPropertyRequest()
    {
        var packet = new
        {
            Type = "BUY_PROPERTY",
            Payload = new
            {
                RoomId = GameSession.RoomId,
                Username = PlayerSession.Instance?.Username
            }
        };

        SendPacket(packet);
        lastClientActionStatus = "Sent: Buy";
        UpdateGameStateOverlayText();
        Debug.Log("[NetworkManager] Đã gửi yêu cầu BUY_PROPERTY bằng phím B.");
    }

    public void SendGameChatMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        string trimmedMessage = message.Replace("\r", " ").Replace("\n", " ").Trim();

        if (trimmedMessage.Length > 160)
            trimmedMessage = trimmedMessage.Substring(0, 160);

        var packet = new
        {
            Type = "GAME_CHAT",
            Payload = new
            {
                RoomId = GameSession.RoomId,
                Username = PlayerSession.Instance?.Username,
                Message = trimmedMessage
            }
        };

        SendPacket(packet);
    }

    private void AddGameChatMessage(ChatMessageData chatMessage)
    {
        if (chatMessage == null || string.IsNullOrWhiteSpace(chatMessage.Message))
            return;

        if (string.IsNullOrWhiteSpace(chatMessage.Username))
            chatMessage.Username = "Player";

        gameChatMessages.Add(chatMessage);

        while (gameChatMessages.Count > MaxChatMessages)
            gameChatMessages.RemoveAt(0);

        GameChatMessageReceived?.Invoke(chatMessage);
    }

    public async void StartListeningToServer()
    {
        if (isListening)
        {
            Debug.Log("[NetworkManager] Đã listen rồi, bỏ qua.");
            return;
        }

        if (ServerStream == null || ClientSocket == null)
        {
            Debug.LogWarning("[NetworkManager] Chưa có kết nối server để listen.");
            return;
        }

        isListening = true;
        pendingData = "";

        Debug.Log("[NetworkManager] Bắt đầu lắng nghe server.");

        try
        {
            while (ClientSocket != null && ClientSocket.Connected && ServerStream != null)
            {
                int bytesRead = await ServerStream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead <= 0)
                {
                    break;
                }

                pendingData += Encoding.UTF8.GetString(buffer, 0, bytesRead);

                while (pendingData.Contains("<EOF>"))
                {
                    int eofIndex = pendingData.IndexOf("<EOF>", StringComparison.Ordinal);
                    string packet = pendingData.Substring(0, eofIndex).Trim();

                    pendingData = pendingData.Substring(eofIndex + "<EOF>".Length);

                    if (!string.IsNullOrWhiteSpace(packet))
                    {
                        ProcessServerMessage(packet);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] Vòng lặp lắng nghe kết thúc: {ex.Message}");
        }
        finally
        {
            isListening = false;
        }
    }

    // ============================================================
    // XỬ LÝ GÓI TIN TỪ SERVER
    // ============================================================
    private void ProcessServerMessage(string serverMessage)
    {
        if (string.IsNullOrWhiteSpace(serverMessage))
            return;

        Debug.Log($"[NetworkManager] Nhận: {serverMessage}");

        // Server login hiện tại trả legacy dạng SUCCESS|uid|token.
        // Trong flow hiện tại AuthManager đã đọc phần này trước khi vào Lobby,
        // nên nếu NetworkManager có lỡ nhận thì bỏ qua để tránh parse JSON lỗi.
        if (serverMessage.StartsWith("SUCCESS") || serverMessage.StartsWith("FAIL"))
        {
            Debug.Log($"[NetworkManager] Nhận legacy auth response, bỏ qua: {serverMessage}");
            return;
        }

        try
        {
            JObject data = JObject.Parse(serverMessage);
            string type = data["Type"]?.ToString() ?? "";

            switch (type)
            {
                // ====================================================
                // ROOM CREATED
                // ====================================================
                case "ROOM_CREATED":
                    {
                        string roomId = data["Payload"]?["RoomId"]?.ToString() ?? "";
                        string mapName = data["Payload"]?["MapName"]?.ToString() ?? "";

                        FindObjectOfType<LobbyManager>()?.OnRoomCreatedSuccess(roomId, mapName);
                        break;
                    }

                case "CREATE_ROOM_FAILED":
                    {
                        string message = data["Payload"]?["Message"]?.ToString() ?? "Tạo phòng thất bại.";
                        Debug.LogWarning($"[NetworkManager] CREATE_ROOM_FAILED: {message}");

                        FindObjectOfType<LobbyManager>()?.OnCreateRoomFailed(message);
                        break;
                    }

                // ====================================================
                // ROOM UPDATE
                // ====================================================
                case "ROOM_UPDATE":
                    {
                        List<PlayerSlotData> players = data["Payload"]?["Players"]
                            ?.ToObject<List<PlayerSlotData>>();

                        if (players != null)
                        {
                            FindObjectOfType<LobbyManager>()?.RefreshPlayerList(players);
                        }

                        break;
                    }

                // ====================================================
                // ROOM LIST
                // ====================================================
                case "ROOM_LIST_RESPONSE":
                    {
                        List<RoomSummaryData> rooms = data["Payload"]?["Rooms"]
                            ?.ToObject<List<RoomSummaryData>>();

                        FindObjectOfType<LobbyManager>()?.RefreshRoomList(rooms);
                        break;
                    }

                // ====================================================
                // JOIN ROOM
                // ====================================================
                case "JOIN_ROOM_SUCCESS":
                    {
                        string roomId = data["Payload"]?["RoomId"]?.ToString() ?? "";
                        string mapName = data["Payload"]?["MapName"]?.ToString() ?? "";

                        FindObjectOfType<LobbyManager>()?.OnJoinRoomSuccess(roomId, mapName);
                        break;
                    }

                case "JOIN_ROOM_FAILED":
                    {
                        string message = data["Payload"]?["Message"]?.ToString() ?? "Không thể vào phòng.";
                        FindObjectOfType<LobbyManager>()?.OnJoinRoomFailed(message);
                        break;
                    }

                // ====================================================
                // START GAME
                // ====================================================
                case "START_GAME_FAILED":
                    {
                        string message = data["Payload"]?["Message"]?.ToString() ?? "Không thể bắt đầu game.";
                        FindObjectOfType<LobbyManager>()?.OnStartGameFailed(message);
                        break;
                    }

                case "GAME_STARTING":
                    {
                        string roomId = data["Payload"]?["RoomId"]?.ToString() ?? "";
                        string mapName = data["Payload"]?["MapName"]?.ToString() ?? "";

                        List<PlayerSlotData> players = data["Payload"]?["Players"]
                            ?.ToObject<List<PlayerSlotData>>();

                        GameStateData gameState = data["Payload"]?["GameState"]
                            ?.ToObject<GameStateData>();

                        SyncServerClock(gameState);
                        GameSession.Initialize(roomId, mapName, players, gameState);
                        gameChatMessages.Clear();

                        Debug.Log(
                            $"[NetworkManager] GAME_STARTING Room={roomId}, Map={mapName}, " +
                            $"Players={players?.Count ?? 0}, CurrentTurn={gameState?.CurrentTurnUsername ?? "N/A"}"
                        );

                        UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
                        break;
                    }

                case "GAME_STATE_UPDATE":
                    {
                        string message = data["Payload"]?["Message"]?.ToString() ?? "";
                        GameStateData gameState = data["Payload"]?["GameState"]
                            ?.ToObject<GameStateData>();

                        SyncServerClock(gameState);
                        GameSession.UpdateState(gameState);
                        lastGameActionError = "";
                        lastClientActionStatus = "";
                        UpdateGameStateOverlayText();
                        UpdateGameplayButtons();

                        Debug.Log(
                            $"[NetworkManager] GAME_STATE_UPDATE Room={gameState?.RoomId ?? "N/A"}, " +
                            $"Turn={gameState?.TurnNumber ?? 0}, CurrentTurn={gameState?.CurrentTurnUsername ?? "N/A"}, " +
                            $"Message={message}"
                        );

                        break;
                    }

                case "CHAT_MESSAGE":
                    {
                        ChatMessageData chatMessage = data["Payload"]?.ToObject<ChatMessageData>();
                        AddGameChatMessage(chatMessage);
                        break;
                    }

                case "GAME_ACTION_FAILED":
                    {
                        string message = data["Payload"]?["Message"]?.ToString() ?? "Hành động không hợp lệ.";
                        lastGameActionError = message;
                        lastClientActionStatus = "";
                        UpdateGameStateOverlayText();
                        UpdateGameplayButtons();
                        Debug.LogWarning($"[NetworkManager] GAME_ACTION_FAILED: {message}");
                        break;
                    }

                // ====================================================
                // LEAVE / ROOM CLOSED
                // ====================================================
                case "LEAVE_ROOM_SUCCESS":
                    {
                        FindObjectOfType<LobbyManager>()?.OnLeaveRoomSuccess();
                        break;
                    }

                case "ROOM_CLOSED":
                    {
                        string message = data["Payload"]?["Message"]?.ToString() ?? "Phòng đã bị đóng.";
                        FindObjectOfType<LobbyManager>()?.OnRoomClosed(message);
                        break;
                    }

                // ====================================================
                // PROFILE
                // ====================================================
                case "SUCCESS_PROFILE":
                    {
                        string newUsername = data["Payload"]?["NewUsername"]?.ToString() ?? "";
                        string newAvatarId = data["Payload"]?["NewAvatarId"]?.ToString() ?? "";

                        FindObjectOfType<LobbyManager>()?.OnProfileUpdateSuccess(newUsername, newAvatarId);
                        break;
                    }

                case "FAIL_PROFILE":
                    {
                        string errorMsg = data["Payload"]?["Message"]?.ToString() ?? "Lỗi không xác định";
                        FindObjectOfType<LobbyManager>()?.OnProfileUpdateFailed(errorMsg);
                        break;
                    }

                default:
                    {
                        Debug.Log($"[NetworkManager] Gói tin chưa xử lý: {type}");
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkManager] Lỗi parse JSON: {ex.Message}\nNội dung: {serverMessage}");
        }
    }

    // ============================================================
    // NGẮT KẾT NỐI
    // ============================================================
    public void Disconnect()
    {
        try
        {
            isListening = false;

            ServerStream?.Close();
            ClientSocket?.Close();

            ServerStream = null;
            ClientSocket = null;

            Debug.Log("[NetworkManager] Đã ngắt kết nối.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] Lỗi Disconnect: {ex.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}
