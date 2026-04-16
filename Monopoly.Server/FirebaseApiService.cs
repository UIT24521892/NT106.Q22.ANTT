using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Monopoly.Server
{
    public class FirebaseApiService
    {
        // Gắn Web API Key của dự án Firebase vào đây
        private readonly string API_KEY = "AIzaSyB-xxxxxxxxxxxxxxxxxxxxxxx";
        private readonly HttpClient _http = new HttpClient();

        public async Task<string> AuthenticateUser(string email, string password, bool isLogin)
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

                return $"SUCCESS|{uid}|{jwtToken}";
            }
            else
            {
                var error = JObject.Parse(responseString);
                string errorMsg = error["error"]["message"].ToString();
                return $"FAIL|{errorMsg}";
            }
        }
    }
}