using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text;
using System;
using Newtonsoft.Json;

public class AuthManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private TMP_InputField inpUsername;
    [SerializeField] private TMP_InputField inpEmail;
    [SerializeField] private TMP_InputField inpPassword;

    [SerializeField] private Button btnAction;
    [SerializeField] private TextMeshProUGUI txtActionBtn; // Chữ bên trong nút Action
    [SerializeField] private TextMeshProUGUI txtSwitchBtn; // Chữ bên trong nút Switch

    private bool isLoginMode = true;

    private void Start()
    {
        UpdateUI();
    }

    // Gắn hàm này vào sự kiện OnClick của nút Switch
    public void OnSwitchModeClicked()
    {
        isLoginMode = !isLoginMode;
        UpdateUI();
    }

    private void UpdateUI()
    {
        txtTitle.text = isLoginMode ? "ĐĂNG NHẬP" : "ĐĂNG KÝ TÀI KHOẢN";
        txtActionBtn.text = isLoginMode ? "ĐĂNG NHẬP" : "TẠO TÀI KHOẢN";
        txtSwitchBtn.text = isLoginMode ? "Chưa có tài khoản? Đăng ký ngay" : "Đã có tài khoản? Đăng nhập";

        // Chỉ hiện ô nhập Username khi ở chế độ Đăng ký
        inpUsername.gameObject.SetActive(!isLoginMode);
    }

    // Gắn hàm này vào sự kiện OnClick của nút Action (Đăng nhập/Đăng ký)
    public async void OnActionClicked()
    {
        if (NetworkManager.ServerStream == null)
        {
            Debug.LogError("Chưa kết nối đến Server! Hãy kiểm tra lại.");
            return;
        }

        btnAction.interactable = false;
        txtActionBtn.text = "ĐANG XỬ LÝ...";

        try
        {
            // 1. Tạo gói tin JSON
            var payload = new
            {
                Username = inpUsername.text,
                Email = inpEmail.text,
                Password = inpPassword.text
            };

            var packet = new
            {
                Type = isLoginMode ? "Login" : "Register",
                Payload = JsonConvert.SerializeObject(payload)
            };

            string jsonToSend = JsonConvert.SerializeObject(packet) + "<EOF>";
            byte[] outStream = Encoding.UTF8.GetBytes(jsonToSend);

            // 2. Gửi qua TCP Server
            await NetworkManager.ServerStream.WriteAsync(outStream, 0, outStream.Length);

            // 3. Lắng nghe phản hồi từ Server
            byte[] buffer = new byte[1024];
            int bytesRead = await NetworkManager.ServerStream.ReadAsync(buffer, 0, buffer.Length);

            if (bytesRead > 0)
            {
                string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Replace("<EOF>", "");

                if (response.StartsWith("SUCCESS"))
                {
                    string[] parts = response.Split('|');
                    Debug.Log($"<color=green>THÀNH CÔNG! Chào mừng {parts[3]}</color>");
                    // TODO: Tắt AuthPanel, Bật MainMenu Panel ở đây!
                }
                else
                {
                    Debug.Log($"<color=red>THẤT BẠI: {response.Split('|')[1]}</color>");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Lỗi mạng: " + ex.Message);
        }
        finally
        {
            btnAction.interactable = true;
            UpdateUI(); // Phục hồi lại chữ trên nút
        }
    }
}