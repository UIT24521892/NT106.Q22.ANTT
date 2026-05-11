using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Monopoly.Server
{
    // ============================================================
    //  FirebaseApiService.cs  (PHIÊN BẢN CẬP NHẬT)
    //  Thêm: UpdateUserProfileAsync() — cập nhật Username và AvatarId
    //        vào node USERS/{uid} trên Firebase Realtime Database.
    // ============================================================

    public class FirebaseApiService
    {
        private readonly string API_KEY = "AIzaSyBrkPdAmZGitSBFtqHdvgnrr77iDduLI2g";
        private readonly string DB_URL_BASE = "https://monopoly-nhom4-nt106q22-default-rtdb.firebaseio.com";

        private readonly HttpClient _http = new HttpClient();

        // ──────────────────────────────────────────────────────
        // HÀM CŨ: Xác thực người dùng (giữ nguyên)
        // ──────────────────────────────────────────────────────

        public async Task<string> AuthenticateUser(string email, string password,
                                                   string username, bool isLogin)
        {
            string endpoint = isLogin ? "signInWithPassword" : "signUp";
            string url = $"https://identitytoolkit.googleapis.com/v1/accounts:{endpoint}?key={API_KEY}";

            var requestBody = new { email, password, returnSecureToken = true };
            var content = new StringContent(
                JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(url, content);
            string responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var data = JObject.Parse(responseString);
                string jwtToken = data["idToken"].ToString();
                string uid = data["localId"].ToString();

                if (!isLogin)
                {
                    bool dbSuccess = await CreateUserInDatabase(uid, username, email, jwtToken);
                    if (!dbSuccess)
                        return "FAIL|Tạo tài khoản thành công nhưng lỗi khi khởi tạo dữ liệu Database.";
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

        // ──────────────────────────────────────────────────────
        // HÀM CŨ: Tạo hồ sơ người dùng ban đầu (giữ nguyên)
        // ──────────────────────────────────────────────────────

        private async Task<bool> CreateUserInDatabase(string uid, string username,
                                                      string email, string idToken)
        {
            try
            {
                string dbUrl = $"{DB_URL_BASE}/USERS/{uid}.json?auth={idToken}";

                var userData = new
                {
                    Username = username,
                    Email = email,
                    AvatarId = "avatar_1",        // Avatar mặc định khi đăng ký
                    Money = 2000000,
                    Point = 0,
                    TotalWins = 0,
                    TotalLosses = 0,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var content = new StringContent(
                    JsonConvert.SerializeObject(userData), Encoding.UTF8, "application/json");

                var response = await _http.PutAsync(dbUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB ERROR] CreateUserInDatabase: {ex.Message}");
                return false;
            }
        }

        // ──────────────────────────────────────────────────────
        // HÀM MỚI: Cập nhật hồ sơ người chơi
        // ──────────────────────────────────────────────────────

        /// <summary>
        /// Cập nhật chỉ 2 trường Username và AvatarId tại USERS/{uid} trên Realtime Database.
        /// Dùng PATCH thay vì PUT để không ghi đè các trường khác (Money, Point, v.v.)
        /// </summary>
        /// <param name="uid">Firebase UID của người dùng cần cập nhật</param>
        /// <param name="newUsername">Tên người chơi mới</param>
        /// <param name="newAvatarId">ID avatar mới (VD: "avatar_2")</param>
        /// <param name="idToken">JWT token từ phiên đăng nhập — xác thực quyền ghi Firebase</param>
        /// <returns>"SUCCESS_PROFILE" hoặc "FAIL_PROFILE|{lý do}"</returns>
        public async Task<string> UpdateUserProfileAsync(string uid, string newUsername,
                                                         string newAvatarId, string idToken)
        {
            try
            {
                // Trỏ thẳng đến node USERS/{uid}, kèm auth token để vượt Security Rules
                string dbUrl = $"{DB_URL_BASE}/USERS/{uid}.json?auth={idToken}";

                // Chỉ cập nhật 2 trường — PATCH giữ nguyên các trường còn lại
                var patchData = new
                {
                    Username = newUsername,
                    AvatarId = newAvatarId,
                    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                string jsonBody = JsonConvert.SerializeObject(patchData);

                // Tạo request PATCH thủ công vì HttpClient không có PatchAsync trước .NET 5
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), dbUrl)
                {
                    Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
                };

                var response = await _http.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[PROFILE] Cập nhật thành công cho UID={uid}: " +
                                      $"Username={newUsername}, Avatar={newAvatarId}");
                    return "SUCCESS_PROFILE";
                }
                else
                {
                    // Firebase trả về lý do lỗi trong body
                    Console.WriteLine($"[PROFILE ERROR] Firebase từ chối: {responseBody}");
                    string errorMsg = TryParseFirebaseError(responseBody);
                    return $"FAIL_PROFILE|{errorMsg}";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PROFILE ERROR] Exception: {ex.Message}");
                return $"FAIL_PROFILE|Lỗi kết nối tới Database.";
            }
        }

        // ──────────────────────────────────────────────────────
        // HELPER: Phân tích thông điệp lỗi từ Firebase
        // ──────────────────────────────────────────────────────

        private string TryParseFirebaseError(string responseBody)
        {
            try
            {
                var errObj = JObject.Parse(responseBody);
                return errObj["error"]?.ToString() ?? "Lỗi không xác định từ Database.";
            }
            catch
            {
                return "Lỗi không xác định từ Database.";
            }
        }
    }
}