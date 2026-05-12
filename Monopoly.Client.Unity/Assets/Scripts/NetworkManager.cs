using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// ============================================================
//  NetworkManager.cs  (PHIÊN BẢN CẬP NHẬT)
//  Thay đổi so với bản gốc:
//    • ProcessServerMessage() xử lý thêm SUCCESS_PROFILE và FAIL_PROFILE
//    • Tham chiếu tới LobbyManager thông qua FindObjectOfType<LobbyManager>()
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

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        if (authPanel != null) authPanel.SetActive(false);
        if (splashPanel != null) splashPanel.SetActive(true);
        if (txtStatus != null) txtStatus.text = "Đang kết nối tới Máy chủ TCP...";
        await ConnectToServerAsync();
    }

    private async Task ConnectToServerAsync()
    {
        try
        {
            ClientSocket = new TcpClient();
            await ClientSocket.ConnectAsync("127.0.0.1", 8080);
            ServerStream = ClientSocket.GetStream();

            if (txtStatus != null)
            {
                txtStatus.color = Color.green;
                txtStatus.text = "Kết nối thành công!";
            }
            await Task.Delay(1000);

            if (splashPanel != null) splashPanel.SetActive(false);
            if (authPanel != null) authPanel.SetActive(true);
        }
        catch (Exception ex)
        {
            if (txtStatus != null)
            {
                txtStatus.color = Color.red;
                txtStatus.text = "Lỗi: Không tìm thấy Máy chủ!";
            }
            Debug.LogError($"TCP Error: {ex.Message}");
        }
    }

    // ──────────────────────────────────────────────────────────
    // VÒNG LẶP LẮNG NGHE — Gọi từ LobbyManager.Start()
    // ──────────────────────────────────────────────────────────

    public async void StartListeningToServer()
    {
        if (isListening || ServerStream == null) return;
        isListening = true;
        byte[] buffer = new byte[4096];

        try
        {
            while (ClientSocket.Connected)
            {
                int bytesRead = await ServerStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    // Tách theo <EOF> để xử lý nhiều gói tin đến cùng lúc
                    string raw = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string[] packets = raw.Split(new[] { "<EOF>" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string packet in packets)
                        ProcessServerMessage(packet.Trim());
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[NetworkManager] Vòng lặp lắng nghe kết thúc: {ex.Message}");
            isListening = false;
        }
    }

    // ──────────────────────────────────────────────────────────
    // XỬ LÝ GÓI TIN TỪ SERVER
    // ──────────────────────────────────────────────────────────

    private void ProcessServerMessage(string jsonResponse)
    {
        if (string.IsNullOrWhiteSpace(jsonResponse)) return;

        // ── Gói tin cũ: lệnh bắt đầu game ────────────────────
        if (jsonResponse.Contains("GAME_STARTING"))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
            return;
        }

        // ── Phân tích JSON để điều hướng theo Type ────────────
        try
        {
            var data = JObject.Parse(jsonResponse);
            string type = data["Type"]?.ToString() ?? "";

            switch (type)
            {
                // ── Phòng được tạo thành công ──────────────────
                case "ROOM_CREATED":
                    {
                        string roomId = data["Payload"]?["RoomId"]?.ToString() ?? "";
                        string mapName = data["Payload"]?["MapName"]?.ToString() ?? "";
                        FindObjectOfType<LobbyManager>()?.OnRoomCreatedSuccess(roomId, mapName);
                        break;
                    }

                // ── Cập nhật danh sách người chơi trong phòng ─
                case "ROOM_UPDATE":
                    {
                        var players = data["Payload"]?["Players"]
                            ?.ToObject<System.Collections.Generic.List<PlayerSlotData>>();
                        if (players != null)
                            FindObjectOfType<LobbyManager>()?.RefreshPlayerList(players);
                        break;
                    }

                // ── Cập nhật hồ sơ thành công ─────────────────
                // Server gửi: { "Type": "SUCCESS_PROFILE",
                //               "Payload": { "NewUsername": "...", "NewAvatarId": "..." } }
                case "SUCCESS_PROFILE":
                    {
                        string newUsername = data["Payload"]?["NewUsername"]?.ToString() ?? "";
                        string newAvatarId = data["Payload"]?["NewAvatarId"]?.ToString() ?? "";

                        // Cập nhật phải chạy trên Main Thread — dùng UnityMainThreadDispatcher
                        // hoặc đơn giản hơn: gọi trực tiếp vì ReadAsync đã được await trên main thread trong Unity
                        FindObjectOfType<LobbyManager>()?.OnProfileUpdateSuccess(newUsername, newAvatarId);
                        break;
                    }

                // ── Cập nhật hồ sơ thất bại ───────────────────
                // Server gửi: { "Type": "FAIL_PROFILE",
                //               "Payload": { "Message": "Username đã tồn tại" } }
                case "FAIL_PROFILE":
                    {
                        string errorMsg = data["Payload"]?["Message"]?.ToString() ?? "Lỗi không xác định";
                        FindObjectOfType<LobbyManager>()?.OnProfileUpdateFailed(errorMsg);
                        break;
                    }

                default:
                    Debug.Log($"[NetworkManager] Gói tin không được xử lý: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkManager] Lỗi phân tích JSON: {ex.Message}\nNội dung: {jsonResponse}");
        }
    }

    // ──────────────────────────────────────────────────────────
    // NGẮT KẾT NỐI
    // ──────────────────────────────────────────────────────────

    public void Disconnect()
    {
        ServerStream?.Close();
        ClientSocket?.Close();
        isListening = false;
    }

    private void OnApplicationQuit() => Disconnect();
}