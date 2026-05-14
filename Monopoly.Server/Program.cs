using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Monopoly.Server
{
    class Program
    {
        private static readonly FirebaseApiService _firebaseApi = new FirebaseApiService();

        // State toàn server
        private static readonly Dictionary<NetworkStream, ClientConnection> _clients =
            new Dictionary<NetworkStream, ClientConnection>();

        private static readonly Dictionary<string, Room> _rooms =
            new Dictionary<string, Room>();

        private static readonly object _lock = new object();
        private static readonly Random _random = new Random();

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
                TcpClient tcpClient = await listener.AcceptTcpClientAsync();

                Console.WriteLine($"[CONNECT] Client mới: {tcpClient.Client.RemoteEndPoint}");

                // Không await ở đây để server tiếp tục nhận client khác
                _ = HandleClientAsync(tcpClient);
            }
        }

        // ============================================================
        // HANDLE CLIENT
        // Dùng pending buffer để tránh lỗi TCP bị dính/vỡ packet.
        // ============================================================
        private static async Task HandleClientAsync(TcpClient tcpClient)
        {
            ClientConnection connection = null;

            try
            {
                NetworkStream stream = tcpClient.GetStream();

                connection = new ClientConnection
                {
                    TcpClient = tcpClient,
                    Stream = stream
                };

                lock (_lock)
                {
                    _clients[stream] = connection;
                }

                byte[] buffer = new byte[4096];
                string pending = "";

                while (tcpClient.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    pending += Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    while (pending.Contains("<EOF>"))
                    {
                        int eofIndex = pending.IndexOf("<EOF>", StringComparison.Ordinal);
                        string jsonPacket = pending.Substring(0, eofIndex).Trim();

                        pending = pending.Substring(eofIndex + "<EOF>".Length);

                        if (string.IsNullOrWhiteSpace(jsonPacket))
                        {
                            continue;
                        }

                        Console.WriteLine($"[NHẬN TỪ {tcpClient.Client.RemoteEndPoint}] {jsonPacket}");

                        await RoutePacketAsync(jsonPacket, connection);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI] Kết nối bị ngắt: {ex.Message}");
            }
            finally
            {
                if (connection != null)
                {
                    await HandleDisconnectAsync(connection);
                }

                tcpClient.Close();

                Console.WriteLine("[DISCONNECT] Một client đã rời đi.");
            }
        }

        // ============================================================
        // ROUTER
        // ============================================================
        private static async Task RoutePacketAsync(string jsonPacket, ClientConnection connection)
        {
            try
            {
                JObject packet = JObject.Parse(jsonPacket);
                string packetType = packet["Type"]?.ToString() ?? "";

                switch (packetType)
                {
                    case "Login":
                    case "Register":
                        await HandleAuthAsync(packet, connection);
                        break;

                    case "UPDATE_PROFILE":
                        await HandleUpdateProfileAsync(packet, connection);
                        break;

                    case "CREATE_ROOM":
                        await HandleCreateRoomAsync(packet, connection);
                        break;

                    case "GET_ROOM_LIST":
                        await HandleGetRoomListAsync(connection);
                        break;

                    case "JOIN_ROOM":
                        await HandleJoinRoomAsync(packet, connection);
                        break;

                    case "PLAYER_READY":
                        await HandlePlayerReadyAsync(packet, connection);
                        break;

                    case "START_GAME":
                        await HandleStartGameAsync(packet, connection);
                        break;

                    case "LEAVE_ROOM":
                        await HandleLeaveRoomAsync(connection, sendLeaveSuccess: true);
                        break;

                    case "LOGOUT":
                        await HandleDisconnectAsync(connection);
                        connection.TcpClient?.Close();
                        break;

                    case "DiceRoll":
                        // TODO: Xử lý gameplay tung xúc xắc sau.
                        Console.WriteLine("[GAME] Nhận DiceRoll nhưng chưa xử lý.");
                        break;

                    default:
                        Console.WriteLine($"[CẢNH BÁO] Packet không xác định: {packetType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI XỬ LÝ GÓI TIN] {ex.Message}");
                Console.WriteLine($"[RAW] {jsonPacket}");
            }
        }

        // ============================================================
        // AUTH
        // Giữ response dạng cũ: SUCCESS|uid|token
        // để tương thích với AuthManager.cs hiện tại.
        // ============================================================
        private static async Task HandleAuthAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = GetPayloadObject(packet);

            string email = payload["Email"]?.ToString() ?? "";
            string password = payload["Password"]?.ToString() ?? "";
            string username = payload["Username"]?.ToString() ?? "";
            bool isLogin = packet["Type"]?.ToString() == "Login";

            Console.WriteLine($"[AUTH] Xử lý {(isLogin ? "Đăng nhập" : "Đăng ký")} cho {email}...");

            string result = await _firebaseApi.AuthenticateUser(email, password, username, isLogin);

            if (result.StartsWith("SUCCESS"))
            {
                string[] parts = result.Split('|');

                if (parts.Length >= 3)
                {
                    connection.Uid = parts[1];

                    // Khi login, username có thể rỗng vì ô Username đang ẩn.
                    // Tạm dùng email làm username nếu chưa có username.
                    connection.Username = string.IsNullOrWhiteSpace(username) ? email : username;
                }
            }

            // Gửi response legacy để AuthManager hiện tại đọc được.
            await SendRawStringAsync(connection.Stream, result);

            Console.WriteLine($"[TRẢ VỀ AUTH] {result}");
        }

        // ============================================================
        // UPDATE PROFILE
        // ============================================================
        private static async Task HandleUpdateProfileAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = GetPayloadObject(packet);

            string uid = payload["Uid"]?.ToString() ?? "";
            string idToken = payload["IdToken"]?.ToString() ?? "";
            string newUsername = payload["NewUsername"]?.ToString() ?? "";
            string newAvatarId = payload["NewAvatarId"]?.ToString() ?? "";

            Console.WriteLine($"[PROFILE] UID={uid}, NewUsername={newUsername}, Avatar={newAvatarId}");

            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(idToken))
            {
                await SendJsonPacketAsync(connection.Stream, new
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
                await SendJsonPacketAsync(connection.Stream, new
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

            string dbResult = await _firebaseApi.UpdateUserProfileAsync(
                uid,
                newUsername,
                newAvatarId,
                idToken
            );

            if (dbResult == "SUCCESS_PROFILE")
            {
                lock (_lock)
                {
                    connection.Username = newUsername;

                    // Nếu đang ở trong phòng thì cập nhật tên trong room luôn.
                    if (!string.IsNullOrWhiteSpace(connection.CurrentRoomId) &&
                        _rooms.TryGetValue(connection.CurrentRoomId, out Room room))
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

                await SendJsonPacketAsync(connection.Stream, new
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
                    await BroadcastRoomUpdateAsync(connection.CurrentRoomId);
                }

                Console.WriteLine($"[PROFILE] Cập nhật thành công: {oldUsername} -> {newUsername}");
            }
            else
            {
                string errorMsg = dbResult.Contains("|")
                    ? dbResult.Split('|')[1]
                    : "Lỗi Database không xác định.";

                await SendJsonPacketAsync(connection.Stream, new
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

        // ============================================================
        // CREATE ROOM
        // ============================================================
        private static async Task HandleCreateRoomAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = GetPayloadObject(packet);

            string hostUsername = payload["HostUsername"]?.ToString() ?? connection.Username;
            int maxPlayers = payload["MaxPlayers"]?.Value<int>() ?? 4;
            int botCount = payload["BotCount"]?.Value<int>() ?? 0;
            string mapName = payload["MapName"]?.ToString() ?? "Classic";

            if (string.IsNullOrWhiteSpace(hostUsername))
            {
                await SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "CREATE_ROOM_FAILED",
                    Payload = new
                    {
                        Message = "Không xác định được tên người chơi."
                    }
                });

                return;
            }

            if (!string.IsNullOrWhiteSpace(connection.CurrentRoomId))
            {
                await SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "CREATE_ROOM_FAILED",
                    Payload = new
                    {
                        Message = "Bạn đang ở trong một phòng khác."
                    }
                });

                return;
            }

            // Giới hạn an toàn theo UI hiện tại: 2-4 người.
            if (maxPlayers < 2) maxPlayers = 2;
            if (maxPlayers > 4) maxPlayers = 4;

            if (botCount < 0) botCount = 0;
            if (botCount > maxPlayers - 1) botCount = maxPlayers - 1;

            string roomId;
            Room room;

            lock (_lock)
            {
                roomId = GenerateRoomIdUnsafe();

                connection.Username = hostUsername;
                connection.CurrentRoomId = roomId;

                room = new Room
                {
                    RoomId = roomId,
                    HostUsername = hostUsername,
                    MaxPlayers = maxPlayers,
                    BotCount = botCount,
                    MapName = mapName,
                    IsStarted = false
                };

                room.Players.Add(new RoomPlayer
                {
                    Username = hostUsername,
                    IsReady = true,
                    IsHost = true,
                    IsBot = false,
                    PlayerIndex = 0
                });

                for (int i = 0; i < botCount && room.Players.Count < maxPlayers; i++)
                {
                    room.Players.Add(new RoomPlayer
                    {
                        Username = $"Bot {i + 1}",
                        IsReady = true,
                        IsHost = false,
                        IsBot = true,
                        PlayerIndex = room.Players.Count
                    });
                }

                _rooms[roomId] = room;
            }

            Console.WriteLine($"[ROOM] Tạo phòng {roomId} bởi {hostUsername}. Max={maxPlayers}, Bot={botCount}, Map={mapName}");

            await SendJsonPacketAsync(connection.Stream, new
            {
                Type = "ROOM_CREATED",
                Payload = new
                {
                    RoomId = roomId,
                    MapName = mapName
                }
            });

            await BroadcastRoomUpdateAsync(roomId);
        }

        // ============================================================
        // GET ROOM LIST
        // ============================================================
        private static async Task HandleGetRoomListAsync(ClientConnection connection)
        {
            List<object> roomList = new List<object>();

            lock (_lock)
            {
                foreach (Room room in _rooms.Values)
                {
                    if (room.IsStarted)
                    {
                        continue;
                    }

                    if (room.Players.Count >= room.MaxPlayers)
                    {
                        continue;
                    }

                    roomList.Add(new
                    {
                        RoomId = room.RoomId,
                        HostUsername = room.HostUsername,
                        CurrentPlayers = room.Players.Count,
                        MaxPlayers = room.MaxPlayers,
                        BotCount = room.BotCount,
                        MapName = room.MapName
                    });
                }
            }

            await SendJsonPacketAsync(connection.Stream, new
            {
                Type = "ROOM_LIST_RESPONSE",
                Payload = new
                {
                    Rooms = roomList
                }
            });

            Console.WriteLine($"[ROOM_LIST] Trả về {roomList.Count} phòng.");
        }

        // ============================================================
        // JOIN ROOM
        // ============================================================
        private static async Task HandleJoinRoomAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = GetPayloadObject(packet);

            string roomId = payload["RoomId"]?.ToString() ?? "";
            string username = payload["Username"]?.ToString() ?? connection.Username;

            if (string.IsNullOrWhiteSpace(username))
            {
                await SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "JOIN_ROOM_FAILED",
                    Payload = new
                    {
                        Message = "Không xác định được tên người chơi."
                    }
                });

                return;
            }

            if (!string.IsNullOrWhiteSpace(connection.CurrentRoomId))
            {
                await SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "JOIN_ROOM_FAILED",
                    Payload = new
                    {
                        Message = "Bạn đang ở trong một phòng khác."
                    }
                });

                return;
            }

            string mapName;

            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out Room room))
                {
                    _ = SendJsonPacketAsync(connection.Stream, new
                    {
                        Type = "JOIN_ROOM_FAILED",
                        Payload = new
                        {
                            Message = "Phòng không tồn tại."
                        }
                    });

                    return;
                }

                if (room.IsStarted)
                {
                    _ = SendJsonPacketAsync(connection.Stream, new
                    {
                        Type = "JOIN_ROOM_FAILED",
                        Payload = new
                        {
                            Message = "Phòng đã bắt đầu."
                        }
                    });

                    return;
                }

                if (room.Players.Count >= room.MaxPlayers)
                {
                    _ = SendJsonPacketAsync(connection.Stream, new
                    {
                        Type = "JOIN_ROOM_FAILED",
                        Payload = new
                        {
                            Message = "Phòng đã đầy."
                        }
                    });

                    return;
                }

                if (room.Players.Any(p => p.Username == username))
                {
                    _ = SendJsonPacketAsync(connection.Stream, new
                    {
                        Type = "JOIN_ROOM_FAILED",
                        Payload = new
                        {
                            Message = "Người chơi này đã ở trong phòng."
                        }
                    });

                    return;
                }

                connection.Username = username;
                connection.CurrentRoomId = roomId;

                room.Players.Add(new RoomPlayer
                {
                    Username = username,
                    IsReady = false,
                    IsHost = false,
                    IsBot = false,
                    PlayerIndex = room.Players.Count
                });

                mapName = room.MapName;
            }

            Console.WriteLine($"[ROOM] {username} vào phòng {roomId}");

            await SendJsonPacketAsync(connection.Stream, new
            {
                Type = "JOIN_ROOM_SUCCESS",
                Payload = new
                {
                    RoomId = roomId,
                    MapName = mapName
                }
            });

            await BroadcastRoomUpdateAsync(roomId);
        }

        // ============================================================
        // PLAYER READY
        // ============================================================
        private static async Task HandlePlayerReadyAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = GetPayloadObject(packet);

            bool isReady = payload["IsReady"]?.Value<bool>() ?? false;
            string roomId = payload["RoomId"]?.ToString() ?? connection.CurrentRoomId;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out Room room))
                {
                    return;
                }

                RoomPlayer player = room.Players.FirstOrDefault(
                    p => p.Username == connection.Username && !p.IsBot
                );

                if (player != null)
                {
                    player.IsReady = isReady;
                }
            }

            Console.WriteLine($"[ROOM] {connection.Username} -> Ready={isReady}");

            await BroadcastRoomUpdateAsync(roomId);
        }

        // ============================================================
        // START GAME
        // ============================================================
        private static async Task HandleStartGameAsync(JObject packet, ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "START_GAME_FAILED",
                    Payload = new
                    {
                        Message = "Bạn chưa ở trong phòng nào."
                    }
                });

                return;
            }

            Room roomSnapshot;

            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out Room room))
                {
                    _ = SendJsonPacketAsync(connection.Stream, new
                    {
                        Type = "START_GAME_FAILED",
                        Payload = new
                        {
                            Message = "Phòng không tồn tại."
                        }
                    });

                    return;
                }

                if (room.HostUsername != connection.Username)
                {
                    _ = SendJsonPacketAsync(connection.Stream, new
                    {
                        Type = "START_GAME_FAILED",
                        Payload = new
                        {
                            Message = "Chỉ chủ phòng mới được bắt đầu."
                        }
                    });

                    return;
                }

                bool allReady = room.Players.All(p => p.IsHost || p.IsBot || p.IsReady);

                if (!allReady)
                {
                    _ = SendJsonPacketAsync(connection.Stream, new
                    {
                        Type = "START_GAME_FAILED",
                        Payload = new
                        {
                            Message = "Vẫn còn người chơi chưa sẵn sàng."
                        }
                    });

                    return;
                }

                // Cho phép: host + bot, hoặc host + client.
                if (room.Players.Count < 2)
                {
                    _ = SendJsonPacketAsync(connection.Stream, new
                    {
                        Type = "START_GAME_FAILED",
                        Payload = new
                        {
                            Message = "Cần ít nhất 2 người chơi hoặc bot để bắt đầu."
                        }
                    });

                    return;
                }

                room.IsStarted = true;
                roomSnapshot = room;
            }

            Console.WriteLine($"[GAME] Phòng {roomId} bắt đầu game.");

            await BroadcastGameStartingAsync(roomSnapshot);
        }

        // ============================================================
        // LEAVE ROOM
        // ============================================================
        private static async Task HandleLeaveRoomAsync(ClientConnection connection, bool sendLeaveSuccess)
        {
            string roomId;
            string username;
            bool shouldCloseRoom = false;
            List<ClientConnection> roomClosedTargets = null;

            lock (_lock)
            {
                roomId = connection.CurrentRoomId;
                username = connection.Username;

                if (string.IsNullOrWhiteSpace(roomId))
                {
                    return;
                }

                if (!_rooms.TryGetValue(roomId, out Room room))
                {
                    connection.CurrentRoomId = "";
                    return;
                }

                bool isHostLeaving = room.HostUsername == username;

                room.Players.RemoveAll(p => p.Username == username && !p.IsBot);
                connection.CurrentRoomId = "";

                if (isHostLeaving || room.Players.Count == 0)
                {
                    shouldCloseRoom = true;

                    _rooms.Remove(roomId);

                    roomClosedTargets = _clients.Values
                        .Where(c => c.CurrentRoomId == roomId)
                        .ToList();

                    foreach (ClientConnection client in roomClosedTargets)
                    {
                        client.CurrentRoomId = "";
                    }
                }
            }

            if (sendLeaveSuccess)
            {
                await SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "LEAVE_ROOM_SUCCESS",
                    Payload = new
                    {
                        Message = "Đã rời phòng."
                    }
                });
            }

            if (shouldCloseRoom)
            {
                if (roomClosedTargets != null)
                {
                    foreach (ClientConnection target in roomClosedTargets)
                    {
                        await SendJsonPacketAsync(target.Stream, new
                        {
                            Type = "ROOM_CLOSED",
                            Payload = new
                            {
                                Message = "Chủ phòng đã rời đi. Phòng đã bị đóng."
                            }
                        });
                    }
                }

                Console.WriteLine($"[ROOM] Phòng {roomId} đã bị đóng.");
            }
            else
            {
                await BroadcastRoomUpdateAsync(roomId);

                Console.WriteLine($"[ROOM] {username} rời phòng {roomId}.");
            }
        }

        // ============================================================
        // DISCONNECT
        // ============================================================
        private static async Task HandleDisconnectAsync(ClientConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            bool existed;

            lock (_lock)
            {
                existed = _clients.Remove(connection.Stream);
            }

            if (!existed)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(connection.CurrentRoomId))
            {
                await HandleLeaveRoomAsync(connection, sendLeaveSuccess: false);
            }
        }

        // ============================================================
        // BROADCAST HELPERS
        // ============================================================
        private static async Task BroadcastRoomUpdateAsync(string roomId)
        {
            Room roomSnapshot;

            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out Room room))
                {
                    return;
                }

                roomSnapshot = room;
            }

            await BroadcastToRoomAsync(roomId, new
            {
                Type = "ROOM_UPDATE",
                Payload = new
                {
                    RoomId = roomSnapshot.RoomId,
                    MapName = roomSnapshot.MapName,
                    Players = roomSnapshot.Players
                }
            });
        }

        private static async Task BroadcastGameStartingAsync(Room room)
        {
            await BroadcastToRoomAsync(room.RoomId, new
            {
                Type = "GAME_STARTING",
                Payload = new
                {
                    RoomId = room.RoomId,
                    MapName = room.MapName,
                    Players = room.Players
                }
            });
        }

        private static async Task BroadcastToRoomAsync(string roomId, object packet)
        {
            List<ClientConnection> targets;

            lock (_lock)
            {
                targets = _clients.Values
                    .Where(c => c.CurrentRoomId == roomId)
                    .ToList();
            }

            foreach (ClientConnection target in targets)
            {
                await SendJsonPacketAsync(target.Stream, packet);
            }
        }

        // ============================================================
        // SEND HELPERS
        // ============================================================
        private static async Task SendJsonPacketAsync(NetworkStream stream, object responseObject)
        {
            try
            {
                string json = JsonConvert.SerializeObject(responseObject) + "<EOF>";
                byte[] bytes = Encoding.UTF8.GetBytes(json);

                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            }
            catch
            {
                // Client đã đóng kết nối, bỏ qua.
            }
        }

        private static async Task SendRawStringAsync(NetworkStream stream, string message)
        {
            try
            {
                string data = message + "<EOF>";
                byte[] bytes = Encoding.UTF8.GetBytes(data);

                await stream.WriteAsync(bytes, 0, bytes.Length);
                await stream.FlushAsync();
            }
            catch
            {
                // Client đã đóng kết nối, bỏ qua.
            }
        }

        // ============================================================
        // UTILS
        // ============================================================
        private static JObject GetPayloadObject(JObject packet)
        {
            JToken payloadToken = packet["Payload"];

            if (payloadToken == null)
            {
                return new JObject();
            }

            if (payloadToken.Type == JTokenType.Object)
            {
                return (JObject)payloadToken;
            }

            if (payloadToken.Type == JTokenType.String)
            {
                string payloadString = payloadToken.ToString();

                if (string.IsNullOrWhiteSpace(payloadString))
                {
                    return new JObject();
                }

                return JObject.Parse(payloadString);
            }

            return JObject.FromObject(payloadToken);
        }

        // Chỉ gọi hàm này bên trong lock(_lock).
        private static string GenerateRoomIdUnsafe()
        {
            string roomId;

            do
            {
                roomId = _random.Next(1000, 9999).ToString();
            }
            while (_rooms.ContainsKey(roomId));

            return roomId;
        }
    }
}