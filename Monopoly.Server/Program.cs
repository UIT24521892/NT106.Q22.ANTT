using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Monopoly.Shared.Models.Network; // Mở comment này nếu bạn đã add reference tới project Shared

namespace Monopoly.Server
{
    class Program
    {
        // Khởi tạo dịch vụ Firebase Auth đã viết ở bài trước
        private static FirebaseApiService _firebaseApi = new FirebaseApiService();

        // Đổi Main thành async Task Main để hỗ trợ code bất đồng bộ (Async/Await)
        static async Task Main(string[] args)
        {
            // Thêm 2 dòng này ĐẦU TIÊN để ép Console dùng UTF-8
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            Console.Title = "Monopoly Game Server - Nhóm 4";
            Console.WriteLine("=====================================");
            Console.WriteLine("    MÁY CHỦ CỜ TỶ PHÚ ĐANG KHỞI ĐỘNG ");
            Console.WriteLine("=====================================");

            // Lắng nghe trên mọi IP của máy hiện tại, ở cổng 8080
            TcpListener listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();
            Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - Server đang lắng nghe tại cổng 8080...\n");

            // Vòng lặp VÔ TẬN để liên tục đón khách (Clients)
            while (true)
            {
                // AcceptTcpClientAsync sẽ "đứng đợi" ở đây cho đến khi có 1 Client kết nối vào
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine($"[CONNECT] Client mới vừa tham gia: {client.Client.RemoteEndPoint}");

                // Giao Client này cho một luồng (Task) riêng biệt chăm sóc
                // Dấu "_" để báo cho C# biết ta không cần chờ Task này chạy xong mới lặp tiếp
                _ = HandleClientAsync(client);
            }
        }

        // Hàm chuyên chăm sóc 1 Client cụ thể (Liên tục đọc dữ liệu gửi lên)
        private static async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                // Dùng khối using để tự động ngắt kết nối NetworkStream khi Client thoát
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[4096]; // Bộ đệm 4KB

                    while (true) // Vòng lặp lắng nghe tin nhắn của Client này
                    {
                        // Đọc byte từ mạng vào buffer
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                        // Nếu số byte đọc được = 0, nghĩa là Client đã chủ động ngắt kết nối
                        if (bytesRead == 0) break;

                        // Chuyển byte thành chuỗi
                        string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        // Kỹ thuật Packet Framing: Cắt chuỗi dựa vào dấu <EOF> để tránh dính gói tin
                        string[] jsonPackets = receivedData.Split(new[] { "<EOF>" }, StringSplitOptions.RemoveEmptyEntries);

                        // Xử lý từng gói tin một
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
                // Bắt lỗi khi Client bị rớt mạng đột ngột (rút dây mạng, tắt ngang màn hình)
                Console.WriteLine($"[LỖI] Kết nối với Client bị ngắt đột ngột: {ex.Message}");
            }
            finally
            {
                client.Close();
                Console.WriteLine($"[DISCONNECT] Một Client đã rời đi.");
            }
        }

        // Hàm "Tổng đài viên": Bóc vỏ gói tin và điều hướng xử lý
        private static async Task RoutePacketAsync(string jsonPacket, NetworkStream stream)
        {
            try
            {
                // 1. Phân tích gói tin thô thành Dynamic Object (hoặc dùng NetworkPacket class của Shared)
                dynamic packet = JsonConvert.DeserializeObject(jsonPacket);
                string packetType = packet.Type;

                // 2. Điều hướng dựa theo Type
                switch (packetType)
                {
                    case "Login":
                    case "Register":
                        // Bóc tách payload của Login/Register
                        dynamic authPayload = JsonConvert.DeserializeObject(packet.Payload.ToString());
                        string email = authPayload.Email;
                        string password = authPayload.Password;
                        bool isLogin = (packetType == "Login");

                        Console.WriteLine($"[AUTH] Đang xử lý {(isLogin ? "Đăng nhập" : "Đăng ký")} cho {email}...");

                        // Gọi sang class FirebaseApiService
                        string result = await _firebaseApi.AuthenticateUser(email, password, isLogin);

                        // Trả kết quả ngược lại cho Client
                        byte[] responseBytes = Encoding.UTF8.GetBytes(result + "<EOF>");
                        await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

                        Console.WriteLine($"[TRẢ VỀ] {result}");
                        break;

                    case "DiceRoll":
                        // Gọi hàm xử lý tung xúc xắc ở đây
                        break;

                    default:
                        Console.WriteLine($"[CẢNH BÁO] Nhận được loại Packet không xác định: {packetType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI XỬ LÝ GÓI TIN] {ex.Message}");
            }
        }
    }
}