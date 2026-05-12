using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Monopoly.Server
{
    // ============================================================
    //  Program.cs  (PHIÊN BẢN CẬP NHẬT)
    //  Thay đổi so với bản gốc:
    //    • RoutePacketAsync() thêm case "UPDATE_PROFILE"
    //    • Gọi FirebaseApiService.UpdateUserProfileAsync()
    //    • Trả về gói JSON { Type, Payload } thay vì chuỗi thuần
    //      để NetworkManager phía Client dễ phân tích bằng JObject.Parse()
    // ============================================================

    class Program
    {
        private static FirebaseApiService _firebaseApi = new FirebaseApiService();

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            Console.Title = "Monopoly Game Server - Nhóm 4";
            Console.WriteLine("=====================================");
            Console.WriteLine("    MÁY CHỦ CỜ TỶ PHÚ ĐANG KHỞI ĐỘNG ");
            Console.WriteLine("=====================================");

            TcpListener listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();
            Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - Server đang lắng nghe tại cổng 8080...\n");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine($"[CONNECT] Client mới: {client.Client.RemoteEndPoint}");
                _ = HandleClientAsync(client);
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[4096];
                    while (true)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        string[] jsonPackets = receivedData.Split(
                            new[] { "<EOF>" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var json in jsonPackets)
                        {
                            Console.WriteLine($"[NHẬN TỪ {client.Client.RemoteEndPoint}] {json}");
                            await RoutePacketAsync(json, stream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI] Kết nối bị ngắt đột ngột: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine($"[DISCONNECT] Một Client đã rời đi.");
            }
        }

        private static async Task RoutePacketAsync(string jsonPacket, NetworkStream stream)
        {
            try
            {
                dynamic packet = JsonConvert.DeserializeObject(jsonPacket);
                string packetType = packet.Type;

                switch (packetType)
                {
                    // ──────────────────────────────────────────
                    // ĐĂNG NHẬP / ĐĂNG KÝ (giữ nguyên logic cũ)
                    // ──────────────────────────────────────────
                    case "Login":
                    case "Register":
                        {
                            dynamic authPayload = JsonConvert.DeserializeObject(packet.Payload.ToString());
                            string email = authPayload.Email;
                            string password = authPayload.Password;
                            string username = authPayload.Username ?? "";
                            bool isLogin = (packetType == "Login");

                            Console.WriteLine($"[AUTH] Xử lý {(isLogin ? "Đăng nhập" : "Đăng ký")} cho {email}...");

                            string result = await _firebaseApi.AuthenticateUser(email, password, username, isLogin);

                            // Trả về chuỗi thuần (giữ tương thích với AuthManager.cs gốc)
                            byte[] responseBytes = Encoding.UTF8.GetBytes(result + "<EOF>");
                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

                            Console.WriteLine($"[TRẢ VỀ AUTH] {result}");
                            break;
                        }

                    // ──────────────────────────────────────────
                    // CẬP NHẬT HỒ SƠ CÁ NHÂN  ← MỚI
                    // ──────────────────────────────────────────
                    case "UPDATE_PROFILE":
                        {
                            dynamic profilePayload = JsonConvert.DeserializeObject(packet.Payload.ToString());

                            string uid = profilePayload.Uid?.ToString() ?? "";
                            string idToken = profilePayload.IdToken?.ToString() ?? "";
                            string newUsername = profilePayload.NewUsername?.ToString() ?? "";
                            string newAvatarId = profilePayload.NewAvatarId?.ToString() ?? "";

                            Console.WriteLine($"[PROFILE] Yêu cầu cập nhật từ UID={uid}: " +
                                              $"Username={newUsername}, Avatar={newAvatarId}");

                            // Validate server-side tối thiểu
                            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(idToken))
                            {
                                await SendJsonPacketAsync(stream, new
                                {
                                    Type = "FAIL_PROFILE",
                                    Payload = new { Message = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại." }
                                });
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Length < 3)
                            {
                                await SendJsonPacketAsync(stream, new
                                {
                                    Type = "FAIL_PROFILE",
                                    Payload = new { Message = "Tên người chơi không hợp lệ (tối thiểu 3 ký tự)." }
                                });
                                break;
                            }

                            // Gọi Firebase để cập nhật
                            string dbResult = await _firebaseApi.UpdateUserProfileAsync(
                                uid, newUsername, newAvatarId, idToken);

                            if (dbResult == "SUCCESS_PROFILE")
                            {
                                // Trả về JSON với Type = "SUCCESS_PROFILE" để NetworkManager parse
                                await SendJsonPacketAsync(stream, new
                                {
                                    Type = "SUCCESS_PROFILE",
                                    Payload = new
                                    {
                                        NewUsername = newUsername,
                                        NewAvatarId = newAvatarId
                                    }
                                });
                                Console.WriteLine($"[PROFILE] Cập nhật thành công cho UID={uid}");
                            }
                            else
                            {
                                // dbResult dạng "FAIL_PROFILE|{lý do}"
                                string errorMsg = dbResult.Contains("|")
                                    ? dbResult.Split('|')[1]
                                    : "Lỗi Database không xác định.";

                                await SendJsonPacketAsync(stream, new
                                {
                                    Type = "FAIL_PROFILE",
                                    Payload = new { Message = errorMsg }
                                });
                                Console.WriteLine($"[PROFILE] Cập nhật thất bại: {errorMsg}");
                            }
                            break;
                        }

                    // ──────────────────────────────────────────
                    // CÁC LOẠI PACKET KHÁC
                    // ──────────────────────────────────────────
                    case "DiceRoll":
                        // TODO: Xử lý tung xúc xắc
                        break;

                    default:
                        Console.WriteLine($"[CẢNH BÁO] Packet không xác định: {packetType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI XỬ LÝ GÓI TIN] {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────────────
        // HELPER: Đóng gói object thành JSON + <EOF> rồi gửi đi
        // Dùng chung cho tất cả phản hồi dạng JSON (thay vì string thuần)
        // ──────────────────────────────────────────────────────

        private static async Task SendJsonPacketAsync(NetworkStream stream, object responseObject)
        {
            string json = JsonConvert.SerializeObject(responseObject) + "<EOF>";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await stream.WriteAsync(bytes, 0, bytes.Length);
        }
    }
}