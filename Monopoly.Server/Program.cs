using Monopoly.Shared; // Gọi namespace của project Shared
using Monopoly.Shared.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Monopoly.Server
{
    public class FirebaseAuthService
    {
        // Thay bằng Web API Key thật của dự án Firebase của bạn
        private readonly string API_KEY = "https://monopoly-nhom4-nt106q22-default-rtdb.firebaseio.com/";
        private readonly HttpClient _http = new HttpClient();

        // 1. Hàm Xử lý Đăng Nhập
        public async Task<AuthResponse> LoginAsync(string email, string password)
        {
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={API_KEY}";
            var requestBody = new { email, password, returnSecureToken = true };

            return await SendAuthRequest(url, requestBody);
        }

        // 2. Hàm Xử lý Đăng Ký
        public async Task<AuthResponse> RegisterAsync(string email, string password)
        {
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={API_KEY}";
            var requestBody = new { email, password, returnSecureToken = true };

            var response = await SendAuthRequest(url, requestBody);

            // Nếu đăng ký thành công, tự động gửi luôn email xác 
            if (response.IsSuccess)
            {
                await SendEmailVerificationAsync(response.UID); // response.UID tạm dùng chứa ID Token lúc đăng ký
            }
            return response;
        }

        // 3. Hàm gửi Email Xác Thực (Cần ID Token của user)
        private async Task SendEmailVerificationAsync(string idToken)
        {
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={API_KEY}";
            var requestBody = new { requestType = "VERIFY_EMAIL", idToken = idToken };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            await _http.PostAsync(url, content);
        }

        // 4. Hàm Gửi link Quên Mật Khẩu
        public async Task<AuthResponse> ResetPasswordAsync(string email)
        {
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={API_KEY}";
            var requestBody = new { requestType = "PASSWORD_RESET", email = email };

            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            var res = await _http.PostAsync(url, content);

            return new AuthResponse { IsSuccess = res.IsSuccessStatusCode, Message = res.IsSuccessStatusCode ? "Đã gửi email khôi phục." : "Lỗi gửi email." };
        }

        // Hàm helper chung để gọi API Firebase
        private async Task<AuthResponse> SendAuthRequest(string url, object body)
        {
            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, content);
            string responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JObject.Parse(responseString);
                return new AuthResponse
                {
                    IsSuccess = true,
                    Message = "Thành công",
                    UID = data["localId"]?.ToString() // localId chính là UID
                };
            }
            else
            {
                var error = JObject.Parse(responseString);
                string errorMsg = error["error"]?["message"]?.ToString() ?? "Lỗi không xác định";
                return new AuthResponse { IsSuccess = false, Message = $"Lỗi: {errorMsg}" };
            }
        }
    }
}