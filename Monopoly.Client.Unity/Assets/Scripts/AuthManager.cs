using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text;
using System;
using Newtonsoft.Json;

public class AuthManager : MonoBehaviour
{
    // --- PHẦN SỬA ĐỔI 1: Khai báo các Panel ---
    [Header("Panels")]
    [SerializeField] private GameObject authPanel;     // Login Panel cũ
    [SerializeField] private GameObject registerPanel; // Register Panel mới bạn vừa tạo

    // --- PHẦN SỬA ĐỔI 2: Tách biệt các ô nhập liệu ---
    [Header("Login Fields (AuthPanel)")]
    [SerializeField] private TMP_InputField inpEmail_Login;
    [SerializeField] private TMP_InputField inpPassword_Login;

    [Header("Register Fields (RegisterPanel)")]
    // Lưu ý: Phải chọn đúng loại "Input Field - TextMeshPro" trong Unity
    [SerializeField] private TMP_InputField inpUsername_Reg; 
    [SerializeField] private TMP_InputField inpEmail_Reg;
    [SerializeField] private TMP_InputField inpPassword_Reg;
    [SerializeField] private TMP_InputField inpConfirmPassword_Reg; 

    // Thông báo trạng thái đăng nhâp/đăng ký
    [SerializeField] private TextMeshProUGUI txtStatus;
    [SerializeField] private TextMeshProUGUI txtStatus_Reg;

    // Tạo một hàm để hiển thị thông báo lỗi
    private void ShowMessage(string message, bool isError = true)
    {
        // Xác định cái "loa" nào sẽ phát thông báo
        TextMeshProUGUI targetText = isLoginMode ? txtStatus : txtStatus_Reg;

        if (targetText == null) return;
        
        // Dùng Rich Text để ép màu cho chắc chắn
        string colorHex = isError ? "#FF0000" : "#00FF00";
        targetText.text = $"<color={colorHex}>{message}</color>";

        // Tự động xóa sau 3 giây
        CancelInvoke(nameof(ClearMessage));
        Invoke(nameof(ClearMessage), 3f);
    }

    private void ClearMessage()
    {
        if (txtStatus != null) txtStatus.text = "";
        if (txtStatus_Reg != null) txtStatus_Reg.text = "";
    }

    private bool isLoginMode = true;


    // Trong file AuthManager.cs
    private void Start()
    {
        // 1. Luôn đưa về trạng thái Đăng nhập khi khởi đầu Scene
        isLoginMode = true;

        // 2. Ép buộc hiển thị đúng các Panel để tránh lỗi màn hình trống
        if (authPanel != null) authPanel.SetActive(true);
        if (registerPanel != null) registerPanel.SetActive(false);

        // 3. Xử lý màn hình Splash (màn hình chờ kết nối)
        // Tìm đối tượng SplashPanel trong Scene mới
        GameObject splash = GameObject.Find("SplashPanel");
        
        // Nếu NetworkManager đã kết nối thành công từ trước (trường hợp vừa Logout)
        if (NetworkManager.ServerStream != null && NetworkManager.ClientSocket != null && NetworkManager.ClientSocket.Connected)
        {
            // Tắt luôn SplashPanel vì mạng đã sẵn sàng rồi
            if (splash != null) splash.SetActive(false);
            Debug.Log("AuthManager: Da co ket noi san, tat Splash va hien Login.");
        }
        else
        {
            // Trường hợp mới mở game lần đầu, để SplashPanel hiện lên 
            // cho đến khi NetworkManager kết nối xong và ra lệnh tắt.
            if (splash != null) splash.SetActive(true);
        }
    }
    // --- PHẦN SỬA ĐỔI 3: Các hàm chuyển đổi cho Button ---
    public void OpenRegister()
    {
        isLoginMode = false;
        ClearMessage(); // Xóa thông báo cũ của trang Login trước khi sang Reg
        authPanel.SetActive(false);
        registerPanel.SetActive(true);
    }

    public void OpenLogin()
    {
        isLoginMode = true;
        ClearMessage(); // Xóa thông báo cũ của trang Reg trước khi sang Login
        authPanel.SetActive(true);
        registerPanel.SetActive(false);
    }

    public async void OnActionClicked()
    {
        if (NetworkManager.ServerStream == null) return;

        try
        {
            // --- PHẦN SỬA ĐỔI 4: Lấy dữ liệu tùy theo Mode hiện tại ---
            string email = isLoginMode ? inpEmail_Login.text : inpEmail_Reg.text;
            string password = isLoginMode ? inpPassword_Login.text : inpPassword_Reg.text;
            string username = isLoginMode ? "" : inpUsername_Reg.text;

            // Kiểm tra khớp mật khẩu cho Register
            
            if (!isLoginMode)
            {
                // 1. Kiểm tra các ô có bị bỏ trống không
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ShowMessage("Fill in all the blanks!", true);
                    return;
                }

                // 2. Kiểm tra khớp mật khẩu
                if (password != inpConfirmPassword_Reg.text)
                {
                    ShowMessage("Passwords do not match!", true); // Hiện chữ đỏ ngay trên màn hình
                    return;
                }
            }


            // Gửi dữ liệu lên Server (giữ nguyên logic cũ)
            var payload = new { Username = username, Email = email, Password = password };
            var packet = new { Type = isLoginMode ? "Login" : "Register", Payload = JsonConvert.SerializeObject(payload) };

            string jsonToSend = JsonConvert.SerializeObject(packet) + "<EOF>";
            byte[] outStream = Encoding.UTF8.GetBytes(jsonToSend);
            await NetworkManager.ServerStream.WriteAsync(outStream, 0, outStream.Length);

            // Nhận phản hồi
            byte[] buffer = new byte[1024];
            int bytesRead = await NetworkManager.ServerStream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Replace("<EOF>", "");
                if (response.StartsWith("SUCCESS"))
                {
                    string[] parts = response.Split('|');
                    // Lưu thông tin phiên chơi (UID, Token, Email, Tiền mặc định, Avatar mặc định)
                    PlayerSession.Initialize(parts[1], parts[2], email, 2000000, "avatar_1");
                    UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
                }
                else
                {
                    // Tách lấy nội dung lỗi từ Server (ví dụ: "FAILED|Sai mật khẩu")
                    string detail = response.Split('|')[1]; 
                    ShowMessage(detail, true); // Gọi hàm với tham số true để hiện màu đỏ
                }
            }
        }
        catch (Exception ex) { Debug.LogError(ex.Message); }
    }
}