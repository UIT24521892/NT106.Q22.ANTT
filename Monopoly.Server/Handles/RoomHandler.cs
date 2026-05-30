using Monopoly.Server.Models;
using Monopoly.Server.Models.State;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Monopoly.Server.Network;
using Monopoly.Server.GameLogic;
using Monopoly.Server.Utils;

using System.Threading.Tasks;

namespace Monopoly.Server.Handles
{
    public static class RoomHandler
    {
        public static async Task HandleCreateRoomAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = PacketHelper.GetPayloadObject(packet);

            string hostUsername = payload["HostUsername"]?.ToString() ?? connection.Username;
            int maxPlayers = payload["MaxPlayers"]?.Value<int>() ?? 4;
            int botCount = payload["BotCount"]?.Value<int>() ?? 0;
            string mapName = payload["MapName"]?.ToString() ?? "Classic";

            if (string.IsNullOrWhiteSpace(hostUsername))
            {
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
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

            lock (ServerState.Lock)
            {
                roomId = GameEngine.GenerateRoomIdUnsafe();

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

                ServerState.Rooms[roomId] = room;
            }

            Console.WriteLine($"[ROOM] Tạo phòng {roomId} bởi {hostUsername}. Max={maxPlayers}, Bot={botCount}, Map={mapName}");

            await NetworkSender.SendJsonPacketAsync(connection.Stream, new
            {
                Type = "ROOM_CREATED",
                Payload = new
                {
                    RoomId = roomId,
                    MapName = mapName
                }
            });

            await NetworkSender.BroadcastRoomUpdateAsync(roomId);
        }
        public static async Task HandleGetRoomListAsync(ClientConnection connection)
        {
            List<object> roomList = new List<object>();

            lock (ServerState.Lock)
            {
                foreach (Room room in ServerState.Rooms.Values)
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

            await NetworkSender.SendJsonPacketAsync(connection.Stream, new
            {
                Type = "ROOM_LIST_RESPONSE",
                Payload = new
                {
                    Rooms = roomList
                }
            });

            Console.WriteLine($"[ROOM_LIST] Trả về {roomList.Count} phòng.");
        }
        public static async Task HandleJoinRoomAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = PacketHelper.GetPayloadObject(packet);

            string roomId = payload["RoomId"]?.ToString() ?? "";
            string username = payload["Username"]?.ToString() ?? connection.Username;

            if (string.IsNullOrWhiteSpace(username))
            {
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
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

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room))
                {
                    _ = NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                        _ = NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                    GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                    mapName = room.MapName;
                    rejoinRoomSnapshot = room;
                }
                else if (room.Players.Count >= room.MaxPlayers)
                {
                    _ = NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                    _ = NetworkSender.SendJsonPacketAsync(connection.Stream, new
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

            await NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                await NetworkSender.BroadcastGameStateAsync(roomId, $"{username} đã kết nối lại trận.");
                return;
            }

            await NetworkSender.BroadcastRoomUpdateAsync(roomId);
        }
        public static async Task HandlePlayerReadyAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = PacketHelper.GetPayloadObject(packet);

            bool isReady = payload["IsReady"]?.Value<bool>() ?? false;
            string roomId = payload["RoomId"]?.ToString() ?? connection.CurrentRoomId;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room))
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

            await NetworkSender.BroadcastRoomUpdateAsync(roomId);
        }
        public static async Task HandleStartGameAsync(JObject packet, ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
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

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room))
                {
                    _ = NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                    _ = NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                    _ = NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                    _ = NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                room.GameState = GameEngine.CreateInitialGameState(room);
                roomSnapshot = room;
            }

            Console.WriteLine($"[GAME] Phòng {roomId} bắt đầu game.");
            Console.WriteLine(
                $"[GAME_STATE] Room={roomSnapshot.GameState.RoomId}, " +
                $"Players={roomSnapshot.GameState.Players.Count}, " +
                $"CurrentTurn={roomSnapshot.GameState.CurrentTurnUsername}"
            );

            await NetworkSender.BroadcastGameStartingAsync(roomSnapshot);
            await NetworkSender.BroadcastGameStateAsync(roomSnapshot.RoomId, "Trận đấu đã được khởi tạo.");
        }
        public static async Task HandleGameChatAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = PacketHelper.GetPayloadObject(packet);

            string roomId = payload["RoomId"]?.ToString() ?? connection.CurrentRoomId;
            string message = payload["Message"]?.ToString() ?? "";
            string username = connection.Username;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, "Bạn chưa ở trong phòng để chat.");
                return;
            }

            message = NormalizeChatMessage(message);

            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            bool canSend;

            lock (ServerState.Lock)
            {
                canSend = ServerState.Rooms.TryGetValue(roomId, out Room room) &&
                    room.Players.Any(p => p.Username == username && !p.IsBot) &&
                    connection.CurrentRoomId == roomId;
            }

            if (!canSend)
            {
                await NetworkSender.SendGameActionFailedAsync(connection, "Bạn không ở trong phòng này.");
                return;
            }

            await NetworkSender.BroadcastToRoomAsync(roomId, new
            {
                Type = "CHAT_MESSAGE",
                Payload = new
                {
                    RoomId = roomId,
                    Username = username,
                    Message = message,
                    SentAtUtcTicks = DateTime.UtcNow.Ticks
                }
            });

            Console.WriteLine($"[CHAT] Room={roomId}, User={username}, Message={message}");
        }
        private static string NormalizeChatMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "";
            }

            string normalized = message.Replace("\r", " ").Replace("\n", " ").Trim();

            while (normalized.Contains("  "))
            {
                normalized = normalized.Replace("  ", " ");
            }

            if (normalized.Length > 160)
            {
                normalized = normalized.Substring(0, 160);
            }

            return normalized;
        }
    }
}


