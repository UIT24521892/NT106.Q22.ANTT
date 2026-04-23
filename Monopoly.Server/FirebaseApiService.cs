using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Monopoly.Server
{
    public class FirebaseApiService
    {
        // Gắn Web API Key của dự án Firebase
        private readonly string API_KEY = "AIzaSyBrkPdAmZGitSBFtqHdvgnrr77iDduLI2g";

        // CHÚ Ý: Thay [YOUR_PROJECT_ID] bằng ID dự án Firebase của bạn (VD: monopoly-uit-123)
        private readonly string DB_URL_BASE = "https://monopoly-nhom4-nt106q22-default-rtdb.firebaseio.com";

        private readonly HttpClient _http = new HttpClient();

        public async Task<string> AuthenticateUser(string email, string password, string username, bool isLogin)
        {
            string endpoint = isLogin ? "signInWithPassword" : "signUp";
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:{endpoint}?key={API_KEY}";

            var requestBody = new { email = email, password = password, returnSecureToken = true };
            var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(url, content);
            string responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JObject.Parse(responseString);
                string jwtToken = data["idToken"].ToString();
                string uid = data["localId"].ToString();

                // NẾU LÀ ĐĂNG KÝ: Gọi hàm tạo hồ sơ người chơi trên Realtime Database
                if (!isLogin)
                {
                    bool dbSuccess = await CreateUserInDatabase(uid, username, email, jwtToken);

                    if (!dbSuccess)
                    {
                        return "FAIL|Tạo tài khoản thành công nhưng lỗi khi khởi tạo dữ liệu Database.";
                    }
                }

                return $"SUCCESS|{uid}|{jwtToken}";
            }
            else
            {
                var error = JObject.Parse(responseString);
                string errorMsg = error["error"]["message"].ToString();
                return $"FAIL|{errorMsg}";
            }
        }

        // Hàm phụ xử lý ghi dữ liệu lên Realtime Database
        private async Task<bool> CreateUserInDatabase(string uid, string username, string email, string idToken)
        {
            try
            {
                // Cấu trúc URL trỏ thẳng tới USERS/{uid}. Kèm auth={idToken} để vượt qua Security Rules
                string dbUrl = $"{DB_URL_BASE}/USERS/{uid}.json?auth={idToken}";

                // Khởi tạo các giá trị mặc định cho người chơi mới
                var userData = new
                {
                    Username = username,
                    Email = email,
                    Money = 2000000, // Vốn khởi điểm 2,000,000 như quy định
                    Point = 0,
                    TotalWins = 0,
                    TotalLosses = 0,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string jsonContent = JsonConvert.SerializeObject(userData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // Dùng phương thức PUT để ghi đè hoặc tạo mới chính xác tại ID này
                var response = await _http.PutAsync(dbUrl, content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] {ex.Message}");
                return false;
            }
        }
    }
}