using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Monopoly.Shared.Models.Constants;
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
        private const int TurnDurationSeconds = 45;

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

            _ = RunTurnTimerLoopAsync();

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

        private static async Task RunTurnTimerLoopAsync()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(1000);

                    List<(string RoomId, string Message)> expiredTurns =
                        new List<(string RoomId, string Message)>();

                    lock (_lock)
                    {
                        long nowTicks = DateTime.UtcNow.Ticks;

                        foreach (Room room in _rooms.Values)
                        {
                            if (!room.IsStarted ||
                                room.GameState == null ||
                                room.GameState.IsFinished ||
                                room.GameState.TurnEndsAtUtcTicks <= 0 ||
                                nowTicks < room.GameState.TurnEndsAtUtcTicks)
                            {
                                continue;
                            }

                            GamePlayerState currentPlayer = room.GameState.Players.FirstOrDefault(
                                p => p.PlayerIndex == room.GameState.CurrentTurnPlayerIndex
                            );
                            GamePlayerState nextPlayer = GetNextTurnPlayerUnsafe(room.GameState);

                            room.GameState.CurrentTurnPlayerIndex = nextPlayer.PlayerIndex;
                            room.GameState.CurrentTurnUsername = nextPlayer.Username;
                            room.GameState.TurnNumber++;
                            room.GameState.HasRolledThisTurn = false;
                            ResetTurnTimerUnsafe(room.GameState);

                            string currentUsername = currentPlayer?.Username ?? "Người chơi";
                            room.GameState.LastActionMessage =
                                $"{currentUsername} hết thời gian lượt. Đến lượt {nextPlayer.Username}.";
                            AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                            expiredTurns.Add((room.RoomId, room.GameState.LastActionMessage));

                            Console.WriteLine(
                                $"[TURN_TIMEOUT] Room={room.RoomId}, From={currentUsername}, " +
                                $"Next={nextPlayer.Username}, Turn={room.GameState.TurnNumber}"
                            );
                        }
                    }

                    foreach ((string roomId, string message) in expiredTurns)
                    {
                        await BroadcastGameStateAsync(roomId, message);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TURN_TIMER_ERROR] {ex.Message}");
                }
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
                    case "ROLL_DICE":
                        await HandleDiceRollAsync(connection);
                        break;

                    case "EndTurn":
                    case "END_TURN":
                        await HandleEndTurnAsync(connection);
                        break;

                    case "BUY_PROPERTY":
                    case "BuyProperty":
                        await HandleBuyPropertyAsync(connection);
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
            Room rejoinRoomSnapshot = null;

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
                    GamePlayerState disconnectedPlayer = room.GameState?.Players.FirstOrDefault(
                        p => p.Username == username && !p.IsBot && !p.IsConnected
                    );

                    if (disconnectedPlayer == null)
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

                    disconnectedPlayer.IsConnected = true;
                    connection.Username = username;
                    connection.CurrentRoomId = roomId;
                    room.GameState.LastActionMessage = $"{username} đã kết nối lại trận.";
                    AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                    mapName = room.MapName;
                    rejoinRoomSnapshot = room;
                }
                else if (room.Players.Count >= room.MaxPlayers)
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
                else if (room.Players.Any(p => p.Username == username))
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
                else
                {
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

            if (rejoinRoomSnapshot != null)
            {
                rejoinRoomSnapshot.GameState.ServerUtcTicks = DateTime.UtcNow.Ticks;
                await SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "GAME_STARTING",
                    Payload = new
                    {
                        RoomId = rejoinRoomSnapshot.RoomId,
                        MapName = rejoinRoomSnapshot.MapName,
                        Players = rejoinRoomSnapshot.Players,
                        GameState = rejoinRoomSnapshot.GameState
                    }
                });
                await BroadcastGameStateAsync(roomId, $"{username} đã kết nối lại trận.");
                return;
            }

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
                room.GameState = CreateInitialGameState(room);
                roomSnapshot = room;
            }

            Console.WriteLine($"[GAME] Phòng {roomId} bắt đầu game.");
            Console.WriteLine(
                $"[GAME_STATE] Room={roomSnapshot.GameState.RoomId}, " +
                $"Players={roomSnapshot.GameState.Players.Count}, " +
                $"CurrentTurn={roomSnapshot.GameState.CurrentTurnUsername}"
            );

            await BroadcastGameStartingAsync(roomSnapshot);
            await BroadcastGameStateAsync(roomSnapshot.RoomId, "Trận đấu đã được khởi tạo.");
        }

        // ============================================================
        // LEAVE ROOM
        // ============================================================
        private static async Task HandleDiceRollAsync(ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await SendGameActionFailedAsync(connection, "Bạn chưa ở trong trận nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;

            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Trận đấu không tồn tại hoặc chưa bắt đầu.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Trận đấu đã kết thúc. Người thắng: {room.GameState.WinnerUsername}.";
                }
                else
                {
                    List<string> actionMessages = new List<string>();
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm thấy người chơi trong trận.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chưa đến lượt của bạn. Hiện tại là lượt của {room.GameState.CurrentTurnUsername}.";
                    }
                    else if (room.GameState.HasRolledThisTurn)
                    {
                        failMessage = "Bạn đã đổ xúc xắc trong lượt này. Hãy kết thúc lượt.";
                    }
                    else if (player.JailTurnsLeft > 0)
                    {
                        player.JailTurnsLeft--;
                        room.GameState.LastDice1 = 0;
                        room.GameState.LastDice2 = 0;
                        room.GameState.LastDiceTotal = 0;
                        room.GameState.HasRolledThisTurn = true;

                        actionMessages.Add($"{player.Username} đang ở Đảo Hoang và bị mất lượt này.");
                    }
                    else
                    {
                        int boardSize = BoardDatabase.Squares.Count;
                        int dice1 = _random.Next(1, 7);
                        int dice2 = _random.Next(1, 7);
                        int diceTotal = dice1 + dice2;
                        int oldPosition = player.Position;
                        int rawPosition = oldPosition + diceTotal;
                        int newPosition = rawPosition % boardSize;
                        actionMessages.Add(
                            $"{player.Username} đổ {dice1} + {dice2} = {diceTotal}, đi từ ô {oldPosition} đến ô {newPosition}."
                        );

                        player.Position = newPosition;

                        if (rawPosition >= boardSize)
                        {
                            const long startBonus = 300000;
                            player.Money += startBonus;
                            actionMessages.Add($"{player.Username} đi qua Bắt Đầu và nhận {startBonus:N0}.");
                        }

                        if (room.GameState.Properties.TryGetValue(newPosition, out GamePropertyState landedProperty) &&
                            landedProperty.OwnerPlayerIndex >= 0 &&
                            landedProperty.OwnerPlayerIndex != player.PlayerIndex)
                        {
                            GamePlayerState owner = room.GameState.Players.FirstOrDefault(
                                p => p.PlayerIndex == landedProperty.OwnerPlayerIndex
                            );

                            if (owner != null &&
                                BoardDatabase.Squares.TryGetValue(newPosition, out var landedSquare) &&
                                landedSquare.RentPrices.Count > 0)
                            {
                                long rent = landedSquare.RentPrices[0] * landedProperty.Multiplier;

                                player.Money -= rent;
                                owner.Money += rent;

                                actionMessages.Add(
                                    $"{player.Username} trả {rent:N0} tiền thuê {landedProperty.Name} cho {owner.Username}."
                                );

                                Console.WriteLine(
                                    $"[RENT] Room={roomId}, From={player.Username}, To={owner.Username}, " +
                                    $"Property={landedProperty.Name}, Rent={rent}, " +
                                    $"PayerMoney={player.Money}, OwnerMoney={owner.Money}"
                                );
                            }
                        }

                        ApplySpecialSquareEffectUnsafe(room.GameState, player, newPosition, actionMessages);

                        room.GameState.LastDice1 = dice1;
                        room.GameState.LastDice2 = dice2;
                        room.GameState.LastDiceTotal = diceTotal;
                        Console.WriteLine(
                            $"[DICE] Room={roomId}, Player={player.Username}, " +
                            $"Dice={dice1}+{dice2}, Position={oldPosition}->{newPosition}, Money={player.Money}"
                        );
                    }

                    if (string.IsNullOrWhiteSpace(failMessage))
                    {
                        room.GameState.HasRolledThisTurn = true;
                        ResolveBankruptcyAndWinnerUnsafe(room.GameState, player, actionMessages);
                        room.GameState.LastActionMessage = string.Join(" ", actionMessages);
                        AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                        broadcastMessage = room.GameState.LastActionMessage;
                        shouldBroadcast = true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            if (shouldBroadcast)
            {
                await BroadcastGameStateAsync(roomId, broadcastMessage);
            }
        }

        private static async Task HandleEndTurnAsync(ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await SendGameActionFailedAsync(connection, "Bạn chưa ở trong trận nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;

            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Trận đấu không tồn tại hoặc chưa bắt đầu.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Trận đấu đã kết thúc. Người thắng: {room.GameState.WinnerUsername}.";
                }
                else
                {
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm thấy người chơi trong trận.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chưa đến lượt của bạn. Hiện tại là lượt của {room.GameState.CurrentTurnUsername}.";
                    }
                    else if (!room.GameState.HasRolledThisTurn)
                    {
                        failMessage = "Bạn cần đổ xúc xắc trước khi kết thúc lượt.";
                    }
                    else
                    {
                        GamePlayerState nextPlayer = GetNextTurnPlayerUnsafe(room.GameState);

                        room.GameState.CurrentTurnPlayerIndex = nextPlayer.PlayerIndex;
                        room.GameState.CurrentTurnUsername = nextPlayer.Username;
                        room.GameState.TurnNumber++;
                        room.GameState.HasRolledThisTurn = false;
                        ResetTurnTimerUnsafe(room.GameState);
                        room.GameState.LastActionMessage =
                            $"{player.Username} kết thúc lượt. Đến lượt {nextPlayer.Username}.";
                        AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                        broadcastMessage = room.GameState.LastActionMessage;
                        shouldBroadcast = true;

                        Console.WriteLine(
                            $"[END_TURN] Room={roomId}, From={player.Username}, " +
                            $"Next={nextPlayer.Username}, Turn={room.GameState.TurnNumber}"
                        );
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            if (shouldBroadcast)
            {
                await BroadcastGameStateAsync(roomId, broadcastMessage);
            }
        }

        private static async Task HandleBuyPropertyAsync(ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await SendGameActionFailedAsync(connection, "Bạn chưa ở trong trận nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;

            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Trận đấu không tồn tại hoặc chưa bắt đầu.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Trận đấu đã kết thúc. Người thắng: {room.GameState.WinnerUsername}.";
                }
                else
                {
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm thấy người chơi trong trận.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chưa đến lượt của bạn. Hiện tại là lượt của {room.GameState.CurrentTurnUsername}.";
                    }
                    else if (!room.GameState.HasRolledThisTurn)
                    {
                        failMessage = "Bạn cần đổ xúc xắc trước khi mua đất.";
                    }
                    else if (!room.GameState.Properties.TryGetValue(player.Position, out GamePropertyState property))
                    {
                        failMessage = "Không tìm thấy thông tin ô hiện tại.";
                    }
                    else if (property.Type != "City" && property.Type != "Resort")
                    {
                        failMessage = $"Ô {property.Name} không thể mua.";
                    }
                    else if (property.OwnerPlayerIndex >= 0)
                    {
                        failMessage = $"Ô {property.Name} đã có chủ.";
                    }
                    else if (property.BuyPrice <= 0)
                    {
                        failMessage = $"Ô {property.Name} chưa có giá mua hợp lệ.";
                    }
                    else if (player.Money < property.BuyPrice)
                    {
                        failMessage = $"Bạn không đủ tiền để mua {property.Name}.";
                    }
                    else
                    {
                        player.Money -= property.BuyPrice;
                        property.OwnerPlayerIndex = player.PlayerIndex;

                        room.GameState.LastActionMessage =
                            $"{player.Username} đã mua {property.Name} với giá {property.BuyPrice:N0}.";
                        AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                        broadcastMessage = room.GameState.LastActionMessage;
                        shouldBroadcast = true;

                        Console.WriteLine(
                            $"[BUY_PROPERTY] Room={roomId}, Player={player.Username}, " +
                            $"Property={property.Name}, Position={property.PositionIndex}, " +
                            $"Price={property.BuyPrice}, MoneyLeft={player.Money}"
                        );
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            if (shouldBroadcast)
            {
                await BroadcastGameStateAsync(roomId, broadcastMessage);
            }
        }

        private static async Task HandleLeaveRoomAsync(ClientConnection connection, bool sendLeaveSuccess)
        {
            string roomId;
            string username;
            bool shouldCloseRoom = false;
            bool shouldBroadcastGameState = false;
            string gameStateMessage = "";
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

                if (room.IsStarted && room.GameState != null)
                {
                    GamePlayerState gamePlayer = room.GameState.Players.FirstOrDefault(
                        p => p.Username == username && !p.IsBot
                    );

                    if (gamePlayer != null)
                    {
                        gamePlayer.IsConnected = false;
                    }

                    connection.CurrentRoomId = "";

                    gameStateMessage = $"{username} đã mất kết nối/rời trận.";

                    List<GamePlayerState> connectedHumans = room.GameState.Players
                        .Where(p => !p.IsBot && !p.IsBankrupt && p.IsConnected)
                        .OrderBy(p => p.PlayerIndex)
                        .ToList();

                    if (connectedHumans.Count == 1)
                    {
                        room.GameState.IsFinished = true;
                        room.GameState.WinnerUsername = connectedHumans[0].Username;
                        room.GameState.HasRolledThisTurn = true;
                        room.GameState.TurnEndsAtUtcTicks = 0;
                        gameStateMessage += $" {connectedHumans[0].Username} thắng trận.";
                    }
                    else if (gamePlayer != null &&
                        room.GameState.CurrentTurnPlayerIndex == gamePlayer.PlayerIndex &&
                        !room.GameState.IsFinished)
                    {
                        GamePlayerState nextPlayer = GetNextTurnPlayerUnsafe(room.GameState);

                        room.GameState.CurrentTurnPlayerIndex = nextPlayer.PlayerIndex;
                        room.GameState.CurrentTurnUsername = nextPlayer.Username;
                        room.GameState.TurnNumber++;
                        room.GameState.HasRolledThisTurn = false;
                        ResetTurnTimerUnsafe(room.GameState);
                        gameStateMessage += $" Đến lượt {nextPlayer.Username}.";
                    }

                    room.GameState.LastActionMessage = gameStateMessage;
                    AddGameLogUnsafe(room.GameState, gameStateMessage);
                    shouldBroadcastGameState = true;
                }
                else
                {
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

            if (shouldBroadcastGameState)
            {
                await BroadcastGameStateAsync(roomId, gameStateMessage);
                Console.WriteLine($"[GAME] {username} rời trận {roomId}.");
                return;
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
            room.GameState.ServerUtcTicks = DateTime.UtcNow.Ticks;

            await BroadcastToRoomAsync(room.RoomId, new
            {
                Type = "GAME_STARTING",
                Payload = new
                {
                    RoomId = room.RoomId,
                    MapName = room.MapName,
                    Players = room.Players,
                    GameState = room.GameState
                }
            });
        }

        private static async Task BroadcastGameStateAsync(string roomId, string message)
        {
            GameState gameState;

            lock (_lock)
            {
                if (!_rooms.TryGetValue(roomId, out Room room) || room.GameState == null)
                {
                    return;
                }

                gameState = room.GameState;
                gameState.ServerUtcTicks = DateTime.UtcNow.Ticks;
            }

            await BroadcastToRoomAsync(roomId, new
            {
                Type = "GAME_STATE_UPDATE",
                Payload = new
                {
                    RoomId = roomId,
                    Message = message,
                    GameState = gameState
                }
            });

            Console.WriteLine(
                $"[GAME_STATE_UPDATE] Room={roomId}, " +
                $"Turn={gameState.TurnNumber}, CurrentTurn={gameState.CurrentTurnUsername}, " +
                $"Message={message}"
            );
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
        private static async Task SendGameActionFailedAsync(ClientConnection connection, string message)
        {
            await SendJsonPacketAsync(connection.Stream, new
            {
                Type = "GAME_ACTION_FAILED",
                Payload = new
                {
                    Message = message
                }
            });

            Console.WriteLine($"[GAME_ACTION_FAILED] User={connection.Username}, Message={message}");
        }

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
        private static GameState CreateInitialGameState(Room room)
        {
            GameState gameState = new GameState
            {
                RoomId = room.RoomId,
                MapName = room.MapName,
                TurnNumber = 1,
                CurrentTurnPlayerIndex = room.Players[0].PlayerIndex,
                CurrentTurnUsername = room.Players[0].Username,
                HasRolledThisTurn = false,
                TurnDurationSeconds = TurnDurationSeconds,
                IsFinished = false,
                WinnerUsername = ""
            };

            ResetTurnTimerUnsafe(gameState);

            foreach (RoomPlayer player in room.Players.OrderBy(p => p.PlayerIndex))
            {
                gameState.Players.Add(new GamePlayerState
                {
                    Username = player.Username,
                    IsBot = player.IsBot,
                    PlayerIndex = player.PlayerIndex,
                    Position = 0,
                    Money = 2000000,
                    IsBankrupt = false,
                    IsConnected = !player.IsBot,
                    ConsecutiveDoubles = 0,
                    JailTurnsLeft = 0
                });
            }

            foreach (var kvp in BoardDatabase.Squares)
            {
                var square = kvp.Value;

                gameState.Properties[square.PositionIndex] = new GamePropertyState
                {
                    PositionIndex = square.PositionIndex,
                    Name = square.Name,
                    Type = square.Type,
                    BuyPrice = square.BuyPrice,
                    OwnerPlayerIndex = -1,
                    HouseCount = 0,
                    HasHotel = false,
                    Multiplier = 1,
                    PowerOutageTurn = 0
                };
            }

            return gameState;
        }

        // Chỉ gọi hàm này bên trong lock(_lock) hoặc khi gameState chưa được chia sẻ.
        private static void ResetTurnTimerUnsafe(GameState gameState)
        {
            gameState.TurnDurationSeconds = TurnDurationSeconds;
            gameState.TurnEndsAtUtcTicks = DateTime.UtcNow.AddSeconds(TurnDurationSeconds).Ticks;
            gameState.ServerUtcTicks = DateTime.UtcNow.Ticks;
        }

        // Chỉ gọi hàm này bên trong lock(_lock).
        private static void ApplySpecialSquareEffectUnsafe(
            GameState gameState,
            GamePlayerState player,
            int position,
            List<string> actionMessages)
        {
            if (!BoardDatabase.Squares.TryGetValue(position, out var square))
            {
                return;
            }

            switch (square.Type)
            {
                case "Tax":
                    const long taxAmount = 100000;
                    player.Money -= taxAmount;
                    actionMessages.Add($"{player.Username} vào ô Thuế và nộp {taxAmount:N0}.");
                    break;

                case "Chance":
                    ApplyChanceEffectUnsafe(gameState, player, actionMessages);
                    break;

                case "LostIsland":
                    player.JailTurnsLeft = Math.Max(player.JailTurnsLeft, 1);
                    actionMessages.Add($"{player.Username} vào Đảo Hoang và sẽ mất lượt kế tiếp.");
                    break;

                case "WorldChampionship":
                    const long championshipReward = 150000;
                    player.Money += championshipReward;
                    actionMessages.Add($"{player.Username} nhận thưởng Giải Vô Địch {championshipReward:N0}.");
                    break;

                case "WorldTour":
                    const long tourBonus = 100000;
                    player.Money += tourBonus;
                    actionMessages.Add($"{player.Username} nhận thưởng Du Lịch Thế Giới {tourBonus:N0}.");
                    break;
            }
        }

        // Chỉ gọi hàm này bên trong lock(_lock).
        private static void ApplyChanceEffectUnsafe(
            GameState gameState,
            GamePlayerState player,
            List<string> actionMessages)
        {
            int effect = _random.Next(0, 4);

            switch (effect)
            {
                case 0:
                    const long jackpot = 200000;
                    player.Money += jackpot;
                    actionMessages.Add($"{player.Username} rút thẻ Cơ Hội: Trúng thưởng {jackpot:N0}.");
                    break;

                case 1:
                    const long fine = 100000;
                    player.Money -= fine;
                    actionMessages.Add($"{player.Username} rút thẻ Cơ Hội: Bị phạt {fine:N0}.");
                    break;

                case 2:
                    player.Position = 0;
                    player.Money += 300000;
                    actionMessages.Add($"{player.Username} rút thẻ Cơ Hội: Về ô Bắt Đầu và nhận 300,000.");
                    break;

                default:
                    player.Position = 24;
                    player.JailTurnsLeft = Math.Max(player.JailTurnsLeft, 1);
                    actionMessages.Add($"{player.Username} rút thẻ Cơ Hội: Đi tới Đảo Hoang và mất lượt kế tiếp.");
                    break;
            }
        }

        // Chỉ gọi hàm này bên trong lock(_lock).
        private static void AddGameLogUnsafe(GameState gameState, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            gameState.ActionLog.Add($"Turn {gameState.TurnNumber}: {message}");

            while (gameState.ActionLog.Count > 12)
            {
                gameState.ActionLog.RemoveAt(0);
            }
        }

        // Chỉ gọi hàm này bên trong lock(_lock).
        private static void ResolveBankruptcyAndWinnerUnsafe(
            GameState gameState,
            GamePlayerState currentPlayer,
            List<string> actionMessages)
        {
            if (currentPlayer.Money < 0 && !currentPlayer.IsBankrupt)
            {
                currentPlayer.IsBankrupt = true;
                actionMessages.Add($"{currentPlayer.Username} đã phá sản.");

                Console.WriteLine(
                    $"[BANKRUPT] Player={currentPlayer.Username}, Money={currentPlayer.Money}"
                );
            }

            List<GamePlayerState> activeHumanPlayers = gameState.Players
                .Where(p => !p.IsBankrupt && !p.IsBot && p.IsConnected)
                .OrderBy(p => p.PlayerIndex)
                .ToList();

            if (activeHumanPlayers.Count == 1)
            {
                gameState.IsFinished = true;
                gameState.WinnerUsername = activeHumanPlayers[0].Username;
                gameState.HasRolledThisTurn = true;
                gameState.TurnEndsAtUtcTicks = 0;
                actionMessages.Add($"{gameState.WinnerUsername} thắng trận.");

                Console.WriteLine($"[GAME_OVER] Winner={gameState.WinnerUsername}");
                return;
            }

            if (currentPlayer.IsBankrupt && !gameState.IsFinished)
            {
                GamePlayerState nextPlayer = GetNextTurnPlayerUnsafe(gameState);

                gameState.CurrentTurnPlayerIndex = nextPlayer.PlayerIndex;
                gameState.CurrentTurnUsername = nextPlayer.Username;
                gameState.TurnNumber++;
                gameState.HasRolledThisTurn = false;
                ResetTurnTimerUnsafe(gameState);

                actionMessages.Add($"Đến lượt {nextPlayer.Username}.");

                Console.WriteLine(
                    $"[AUTO_END_TURN] Bankrupt={currentPlayer.Username}, " +
                    $"Next={nextPlayer.Username}, Turn={gameState.TurnNumber}"
                );
            }
        }

        // Chỉ gọi hàm này bên trong lock(_lock).
        private static GamePlayerState GetNextTurnPlayerUnsafe(GameState gameState)
        {
            List<GamePlayerState> activePlayers = gameState.Players
                .Where(p => !p.IsBankrupt && !p.IsBot && p.IsConnected)
                .OrderBy(p => p.PlayerIndex)
                .ToList();

            if (activePlayers.Count == 0)
            {
                activePlayers = gameState.Players
                    .Where(p => !p.IsBankrupt && p.IsConnected)
                    .OrderBy(p => p.PlayerIndex)
                    .ToList();
            }

            if (activePlayers.Count == 0)
            {
                return gameState.Players.OrderBy(p => p.PlayerIndex).First();
            }

            GamePlayerState nextPlayer = activePlayers
                .FirstOrDefault(p => p.PlayerIndex > gameState.CurrentTurnPlayerIndex);

            return nextPlayer ?? activePlayers[0];
        }

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
