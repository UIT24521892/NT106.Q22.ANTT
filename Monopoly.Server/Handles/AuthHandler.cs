using Monopoly.Server.Models;
using Monopoly.Server.Models.Events;
using Monopoly.Server.Models.State;
using Monopoly.Server.Network;
using Monopoly.Server.Services;
using Monopoly.Server.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Monopoly.Server.Handles
{
    public static class AuthHandler
    {
        public static async Task HandleAuthAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = PacketHelper.GetPayloadObject(packet);

            string email = payload["Email"]?.ToString() ?? "";
            string password = payload["Password"]?.ToString() ?? "";
            string username = payload["Username"]?.ToString() ?? "";
            bool isLogin = packet["Type"]?.ToString() == "Login";

            Console.WriteLine($"[AUTH] Xử lý {(isLogin ? "Đăng nhập" : "Đăng ký")} cho {email}...");

            string result = await ServiceLocator.FirebaseApi.AuthenticateUser(email, password, username, isLogin);

            if (result.StartsWith("SUCCESS"))
            {
                string[] parts = result.Split('|');

                if (parts.Length >= 3)
                {
                    connection.Uid = parts[1];
                    connection.IdToken = parts[2];

                    // Khi login, username có thể rỗng vì ô Username đang ẩn.
                    // Tạm dùng email làm username nếu chưa có username.
                    connection.Username = string.IsNullOrWhiteSpace(username) ? email : username;
                }
            }

            // Gửi response legacy để AuthManager hiện tại đọc được.
            await NetworkSender.SendRawStringAsync(connection.Stream, result);

            Console.WriteLine($"[TRẢ VỀ AUTH] {result}");
        }

        public static async Task HandleUpdateProfileAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = PacketHelper.GetPayloadObject(packet);

            string uid = payload["Uid"]?.ToString() ?? "";
            string idToken = payload["IdToken"]?.ToString() ?? "";
            string newUsername = payload["NewUsername"]?.ToString() ?? "";
            string newAvatarId = payload["NewAvatarId"]?.ToString() ?? "";

            Console.WriteLine($"[PROFILE] UID={uid}, NewUsername={newUsername}, Avatar={newAvatarId}");

            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(idToken))
            {
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "FAIL_PROFILE",
                    Payload = new
                    {
                        Message = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại."
                    }
                });

                return;
            }

            if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Length < 3)
            {
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "FAIL_PROFILE",
                    Payload = new
                    {
                        Message = "Tên người chơi không hợp lệ, tối thiểu 3 ký tự."
                    }
                });

                return;
            }

            string oldUsername = connection.Username;

            string dbResult = await ServiceLocator.FirebaseApi.UpdateUserProfileAsync(
                uid,
                newUsername,
                newAvatarId,
                idToken
            );

            if (dbResult == "SUCCESS_PROFILE")
            {
                lock (ServerState.Lock)
                {
                    connection.Username = newUsername;

                    // Nếu đang ở trong phòng thì cập nhật tên trong room luôn.
                    if (!string.IsNullOrWhiteSpace(connection.CurrentRoomId) &&
                        ServerState.Rooms.TryGetValue(connection.CurrentRoomId, out Room room))
                    {
                        RoomPlayer player = room.Players.FirstOrDefault(p => p.Username == oldUsername && !p.IsBot);

                        if (player != null)
                        {
                            player.Username = newUsername;
                        }

                        if (room.HostUsername == oldUsername)
                        {
                            room.HostUsername = newUsername;
                        }
                    }
                }

                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "SUCCESS_PROFILE",
                    Payload = new
                    {
                        NewUsername = newUsername,
                        NewAvatarId = newAvatarId
                    }
                });

                if (!string.IsNullOrWhiteSpace(connection.CurrentRoomId))
                {
                    await NetworkSender.BroadcastRoomUpdateAsync(connection.CurrentRoomId);
                }

                Console.WriteLine($"[PROFILE] Cập nhật thành công: {oldUsername} -> {newUsername}");
            }
            else
            {
                string errorMsg = dbResult.Contains("|")
                    ? dbResult.Split('|')[1]
                    : "Lỗi Database không xác định.";

                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "FAIL_PROFILE",
                    Payload = new
                    {
                        Message = errorMsg
                    }
                });

                Console.WriteLine($"[PROFILE] Cập nhật thất bại: {errorMsg}");
            }
        }

        public static async Task HandleGetLeaderboardAsync(ClientConnection connection)
        {
            List<LeaderboardEntry> entries = await ServiceLocator.FirebaseApi.GetLeaderboardAsync(connection.IdToken, 10);

            await NetworkSender.SendJsonPacketAsync(connection.Stream, new
            {
                Type = "LEADERBOARD_DATA",
                Payload = new
                {
                    Entries = entries
                }
            });
        }
    }
}
