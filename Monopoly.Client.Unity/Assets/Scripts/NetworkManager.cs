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
    private Canvas gameStateOverlayCanvas;
    private TextMeshProUGUI gameStateOverlayText;
    private TextMeshProUGUI gameActionLogText;
    private TextMeshProUGUI gameErrorText;
    private Button rollButton;
    private Button buyButton;
    private Button endTurnButton;
    private string lastGameActionError = "";
    private long serverClockOffsetTicks = 0;

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
            SetGameStateOverlayVisible(false);
            return;
        }

        EnsureGameStateOverlay();
        SetGameStateOverlayVisible(true);
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
        if (gameStateOverlayText != null)
            return;

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GameScene")
            return;

        GameStateData state = GameSession.CurrentState;

        if (state == null)
            return;

        GUI.Box(new Rect(12, 12, 430, 210), "");
        GUILayout.BeginArea(new Rect(24, 20, 406, 194));

        GUILayout.Label($"Room: {state.RoomId} | Turn: {state.TurnNumber}");
        GUILayout.Label($"Time Left: {GetRemainingTurnSeconds(state)}s");

        if (state.IsFinished)
        {
            GUILayout.Label($"Game Over | Winner: {state.WinnerUsername}");
        }
        else
        {
            GUILayout.Label($"Current Turn: {state.CurrentTurnUsername}");
            GUILayout.Label($"Rolled This Turn: {(state.HasRolledThisTurn ? "Yes" : "No")}");
        }

        if (state.LastDiceTotal > 0)
        {
            GUILayout.Label($"Dice: {state.LastDice1} + {state.LastDice2} = {state.LastDiceTotal}");
        }

        if (!string.IsNullOrWhiteSpace(state.LastActionMessage))
        {
            GUILayout.Label(state.LastActionMessage);
        }

        if (state.Players != null)
        {
            foreach (GamePlayerStateData player in state.Players)
            {
                string status = player.IsBankrupt ? "BANKRUPT" : "ACTIVE";
                GUILayout.Label($"{player.Username} | Pos {player.Position} | Money {player.Money:N0} | {status}");
            }
        }

        GUILayout.EndArea();
    }

    private void EnsureGameStateOverlay()
    {
        if (gameStateOverlayCanvas != null)
            return;

        GameObject overlayRoot = new GameObject("RuntimeGameStateOverlay");
        DontDestroyOnLoad(overlayRoot);

        gameStateOverlayCanvas = overlayRoot.AddComponent<Canvas>();
        gameStateOverlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        gameStateOverlayCanvas.sortingOrder = 1000;

        CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        overlayRoot.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(overlayRoot.transform, false);

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.72f);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(16f, -16f);
        panelRect.sizeDelta = new Vector2(760f, 520f);

        GameObject textObject = new GameObject("StateText", typeof(RectTransform));
        textObject.transform.SetParent(panel.transform, false);

        gameStateOverlayText = textObject.AddComponent<TextMeshProUGUI>();
        gameStateOverlayText.color = Color.white;
        gameStateOverlayText.fontSize = 24f;
        gameStateOverlayText.enableWordWrapping = true;
        gameStateOverlayText.richText = false;
        gameStateOverlayText.alignment = TextAlignmentOptions.TopLeft;

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0.34f);
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 14f);
        textRect.offsetMax = new Vector2(-18f, -14f);

        rollButton = CreateRuntimeButton(panel.transform, "RollButton", "Roll", new Vector2(18f, 110f), SendDiceRollRequest);
        buyButton = CreateRuntimeButton(panel.transform, "BuyButton", "Buy", new Vector2(170f, 110f), SendBuyPropertyRequest);
        endTurnButton = CreateRuntimeButton(panel.transform, "EndTurnButton", "End Turn", new Vector2(322f, 110f), SendEndTurnRequest);

        GameObject errorObject = new GameObject("ErrorText", typeof(RectTransform));
        errorObject.transform.SetParent(panel.transform, false);

        gameErrorText = errorObject.AddComponent<TextMeshProUGUI>();
        gameErrorText.color = new Color(1f, 0.82f, 0.25f, 1f);
        gameErrorText.fontSize = 22f;
        gameErrorText.enableWordWrapping = true;
        gameErrorText.alignment = TextAlignmentOptions.TopLeft;

        RectTransform errorRect = errorObject.GetComponent<RectTransform>();
        errorRect.anchorMin = new Vector2(0f, 0f);
        errorRect.anchorMax = new Vector2(1f, 0f);
        errorRect.pivot = new Vector2(0f, 0f);
        errorRect.offsetMin = new Vector2(18f, 70f);
        errorRect.offsetMax = new Vector2(-18f, 108f);

        GameObject logObject = new GameObject("ActionLogText", typeof(RectTransform));
        logObject.transform.SetParent(panel.transform, false);

        gameActionLogText = logObject.AddComponent<TextMeshProUGUI>();
        gameActionLogText.color = new Color(0.86f, 0.92f, 1f, 1f);
        gameActionLogText.fontSize = 18f;
        gameActionLogText.enableWordWrapping = true;
        gameActionLogText.alignment = TextAlignmentOptions.TopLeft;

        RectTransform logRect = logObject.GetComponent<RectTransform>();
        logRect.anchorMin = new Vector2(0f, 0f);
        logRect.anchorMax = new Vector2(1f, 0.32f);
        logRect.offsetMin = new Vector2(18f, 14f);
        logRect.offsetMax = new Vector2(-18f, -48f);
    }

    private Button CreateRuntimeButton(Transform parent, string objectName, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.14f, 0.42f, 0.82f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(138f, 46f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        TextMeshProUGUI text = labelObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.color = Color.white;
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        return button;
    }

    private void SetGameStateOverlayVisible(bool isVisible)
    {
        if (gameStateOverlayCanvas == null)
            return;

        if (gameStateOverlayCanvas.gameObject.activeSelf != isVisible)
            gameStateOverlayCanvas.gameObject.SetActive(isVisible);
    }

    private void UpdateGameStateOverlayText()
    {
        if (gameStateOverlayText == null)
            return;

        GameStateData state = GameSession.CurrentState;

        if (state == null)
        {
            gameStateOverlayText.text = "Dang cho trang thai game...";
            if (gameActionLogText != null)
                gameActionLogText.text = "";
            if (gameErrorText != null)
                gameErrorText.text = "";
            return;
        }

        gameStateOverlayText.text = BuildGameStateOverlayText(state);

        if (gameActionLogText != null)
            gameActionLogText.text = BuildActionLogText(state);

        if (gameErrorText != null)
            gameErrorText.text = lastGameActionError;
    }

    private string BuildGameStateOverlayText(GameStateData state)
    {
        StringBuilder builder = new StringBuilder();

        builder.AppendLine($"Room: {state.RoomId} | Turn: {state.TurnNumber} | Time: {GetRemainingTurnSeconds(state)}s");

        if (state.IsFinished)
        {
            builder.AppendLine($"Game Over | Winner: {state.WinnerUsername}");
        }
        else
        {
            builder.AppendLine($"Current Turn: {state.CurrentTurnUsername}");
            builder.AppendLine($"Rolled: {(state.HasRolledThisTurn ? "Yes - press E to end turn" : "No - press R to roll")}");
        }

        if (state.LastDiceTotal > 0)
        {
            builder.AppendLine($"Dice: {state.LastDice1} + {state.LastDice2} = {state.LastDiceTotal}");
        }

        if (!string.IsNullOrWhiteSpace(state.LastActionMessage))
        {
            builder.AppendLine(state.LastActionMessage);
        }

        if (state.Players != null)
        {
            foreach (GamePlayerStateData player in state.Players)
            {
                string status = player.IsBankrupt ? "BANKRUPT" : (player.IsConnected ? "ACTIVE" : "DISCONNECTED");
                builder.AppendLine($"{player.Username} | Pos {player.Position} | Money {player.Money:N0} | {status}");
            }
        }

        AppendOwnedProperties(builder, state);

        return builder.ToString();
    }

    private string BuildActionLogText(GameStateData state)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Action log:");

        if (state.ActionLog == null || state.ActionLog.Count == 0)
        {
            builder.AppendLine("No actions yet.");
            return builder.ToString();
        }

        int startIndex = Math.Max(0, state.ActionLog.Count - 5);

        for (int i = startIndex; i < state.ActionLog.Count; i++)
        {
            builder.AppendLine(state.ActionLog[i]);
        }

        return builder.ToString();
    }

    private void AppendOwnedProperties(StringBuilder builder, GameStateData state)
    {
        if (state.Properties == null || state.Players == null)
            return;

        builder.AppendLine("Owned:");
        int count = 0;

        foreach (KeyValuePair<int, GamePropertyStateData> propertyPair in state.Properties)
        {
            GamePropertyStateData property = propertyPair.Value;

            if (property == null || property.OwnerPlayerIndex < 0)
                continue;

            string ownerName = GetPlayerNameByIndex(state, property.OwnerPlayerIndex);
            builder.Append($"{property.PositionIndex}:{property.Name}->{ownerName}  ");
            count++;

            if (count >= 5)
                break;
        }

        if (count == 0)
            builder.Append("None");

        builder.AppendLine();
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
        Debug.Log("[NetworkManager] Đã gửi yêu cầu BUY_PROPERTY bằng phím B.");
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

                        Debug.Log(
                            $"[NetworkManager] GAME_STATE_UPDATE Room={gameState?.RoomId ?? "N/A"}, " +
                            $"Turn={gameState?.TurnNumber ?? 0}, CurrentTurn={gameState?.CurrentTurnUsername ?? "N/A"}, " +
                            $"Message={message}"
                        );

                        break;
                    }

                case "GAME_ACTION_FAILED":
                    {
                        string message = data["Payload"]?["Message"]?.ToString() ?? "Hành động không hợp lệ.";
                        lastGameActionError = message;
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
