using UnityEngine;
using TMPro; // Thư viện để chỉnh chữ
using System.Net.Sockets;
using System.Threading.Tasks;
using System;

public class NetworkManager : MonoBehaviour
{
    // Kéo thả TextMeshPro vào đây trên Unity
    [SerializeField] private TextMeshProUGUI txtStatus;

    // Thêm các Panel để bật/tắt (kéo thả vào từ Unity)
    [SerializeField] private GameObject splashPanel;
    [SerializeField] private GameObject authPanel; // Sẽ làm sau

    // Biến static tĩnh để giữ kết nối dùng cho toàn bộ game
    public static TcpClient ClientSocket;
    public static NetworkStream ServerStream;

    // Start được gọi ngay khi mở game lên
    private async void Start()
    {
        // Ẩn panel Auth, hiện panel Splash
        if (authPanel != null) authPanel.SetActive(false);
        splashPanel.SetActive(true);

        txtStatus.text = "Đang kết nối tới Máy chủ TCP...";

        // Gọi hàm kết nối
        await ConnectToServerAsync();
    }

    private async Task ConnectToServerAsync()
    {
        try
        {
            ClientSocket = new TcpClient();

            // Đợi kết nối (Sửa IP thành IP của Server nếu chơi mạng LAN)
            await ClientSocket.ConnectAsync("127.0.0.1", 8080);
            ServerStream = ClientSocket.GetStream();

            txtStatus.color = Color.green;
            txtStatus.text = "Kết nối thành công! Đang tải hệ thống...";

            // Đợi 1 giây cho đẹp mắt
            await Task.Delay(1000);

            splashPanel.SetActive(false); // Tắt màn hình chờ
            if (authPanel != null) authPanel.SetActive(true); // Bật màn hình Đăng nhập
        }
        catch (Exception ex)
        {
            txtStatus.color = Color.red;
            txtStatus.text = "Lỗi: Không tìm thấy Máy chủ!\nHãy kiểm tra xem Server Console đã bật chưa.";
            Debug.LogError($"TCP Error: {ex.Message}");
        }
    }

    // Đảm bảo đóng kết nối khi tắt game
    private void OnApplicationQuit()
    {
        if (ServerStream != null) ServerStream.Close();
        if (ClientSocket != null) ClientSocket.Close();
    }
}