using UnityEngine;
using TMPro;
using System.Net.Sockets;
using System.Threading.Tasks;
using System;
using System.Text;

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

    // Hàm lắng nghe liên tục - Gọi từ LobbyManager
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
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Replace("<EOF>", "");
                    ProcessServerMessage(response);
                }
            }
        }
        catch (Exception) { isListening = false; }
    }

    private void ProcessServerMessage(string jsonResponse)
    {
        // Nhận lệnh chuyển cảnh từ Server
        if (jsonResponse.Contains("GAME_STARTING"))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }
    }

    public void Disconnect()
    {
        ServerStream?.Close();
        ClientSocket?.Close();
    }

    private void OnApplicationQuit() => Disconnect();
}