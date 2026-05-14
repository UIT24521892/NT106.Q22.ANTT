using UnityEngine;
using TMPro;
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

                        GameSession.Initialize(roomId, mapName, players);

                        Debug.Log($"[NetworkManager] GAME_STARTING Room={roomId}, Map={mapName}, Players={players?.Count ?? 0}");

                        UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
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