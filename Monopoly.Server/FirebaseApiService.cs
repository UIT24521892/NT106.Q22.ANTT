using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task<bool> UpdatePlayerMatchResultAsync(
            string uid,
            string idToken,
            string displayName,
            int rank,
            int scoreEarned,
            string matchId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(idToken))
                    return false;

                string userUrl = $"{DB_URL_BASE}/USERS/{uid}.json?auth={idToken}";
                var userResponse = await _http.GetAsync(userUrl);
                string userBody = await userResponse.Content.ReadAsStringAsync();

                int currentPoint = 0;
                int currentWins = 0;
                int currentLosses = 0;

                if (userResponse.IsSuccessStatusCode &&
                    !string.IsNullOrWhiteSpace(userBody) &&
                    userBody != "null")
                {
                    JObject user = JObject.Parse(userBody);
                    currentPoint = user["Point"]?.Value<int>() ?? 0;
                    currentWins = user["TotalWins"]?.Value<int>() ?? 0;
                    currentLosses = user["TotalLosses"]?.Value<int>() ?? 0;
                }

                var patchData = new
                {
                    Username = displayName,
                    Point = currentPoint + scoreEarned,
                    TotalWins = currentWins + (rank == 1 ? 1 : 0),
                    TotalLosses = currentLosses + (rank == 1 ? 0 : 1),
                    UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"), userUrl)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(patchData), Encoding.UTF8, "application/json")
                };

                var patchResponse = await _http.SendAsync(patchRequest);
                string patchBody = await patchResponse.Content.ReadAsStringAsync();

                if (!patchResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[MATCH RESULT ERROR] Firebase patch rejected: {patchBody}");
                    return false;
                }

                string historyUrl = $"{DB_URL_BASE}/USERS/{uid}/MatchHistory/{matchId}.json?auth={idToken}";
                var historyData = new
                {
                    Rank = rank,
                    ScoreEarned = scoreEarned,
                    PlayedAt = DateTime.UtcNow.ToString("O")
                };

                var historyContent = new StringContent(
                    JsonConvert.SerializeObject(historyData), Encoding.UTF8, "application/json");

                var historyResponse = await _http.PutAsync(historyUrl, historyContent);
                string historyBody = await historyResponse.Content.ReadAsStringAsync();

                if (!historyResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[MATCH HISTORY ERROR] Firebase put rejected: {historyBody}");
                    return false;
                }

                Console.WriteLine(
                    $"[MATCH RESULT] User={displayName}, Rank={rank}, ScoreEarned={scoreEarned}, Match={matchId}"
                );

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MATCH RESULT ERROR] Exception: {ex.Message}");
                return false;
            }
        }

        public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(string idToken, int limit = 10)
        {
            List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

            try
            {
                string authQuery = string.IsNullOrWhiteSpace(idToken) ? "" : $"&auth={idToken}";
                string url = $"{DB_URL_BASE}/USERS.json?orderBy=%22Point%22&limitToLast={limit}{authQuery}";

                var response = await _http.GetAsync(url);
                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body) || body == "null")
                {
                    Console.WriteLine($"[LEADERBOARD ERROR] Firebase response: {body}");
                    return entries;
                }

                JObject root = JObject.Parse(body);

                foreach (JProperty property in root.Properties())
                {
                    JObject user = property.Value as JObject;

                    if (user == null)
                        continue;

                    int wins = user["TotalWins"]?.Value<int>() ?? 0;
                    int losses = user["TotalLosses"]?.Value<int>() ?? 0;

                    entries.Add(new LeaderboardEntry
                    {
                        UserId = property.Name,
                        DisplayName = user["Username"]?.ToString() ?? property.Name,
                        Score = user["Point"]?.Value<int>() ?? 0,
                        Wins = wins,
                        TotalMatches = wins + losses
                    });
                }

                return entries
                    .OrderByDescending(e => e.Score)
                    .ThenByDescending(e => e.Wins)
                    .Take(limit)
                    .Select((entry, index) =>
                    {
                        entry.Rank = index + 1;
                        return entry;
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LEADERBOARD ERROR] Exception: {ex.Message}");
                return entries;
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

    public class LeaderboardEntry
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Rank { get; set; }
        public int Score { get; set; }
        public int Wins { get; set; }
        public int TotalMatches { get; set; }
    }
}
