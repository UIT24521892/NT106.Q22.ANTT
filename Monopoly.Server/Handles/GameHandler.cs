using Monopoly.Server.Models;
using Monopoly.Server.Models.Events;
using Monopoly.Server.Models.State;
using Monopoly.Shared.Models.Configs.StaticData;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Monopoly.Server.Network;
using Monopoly.Server.GameLogic;
using Monopoly.Server.Utils;

using System.Threading.Tasks;

namespace Monopoly.Server.Handles
{
    public static partial class GameHandler
    {
        private static int RollDie()
        {
            return RandomNumberGenerator.GetInt32(1, 7);
        }
        public static async Task HandleDiceRollAsync(ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, "Bạn chưa ở trong trận nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;
            List<CardDrawEvent> cardDrawEvents = new List<CardDrawEvent>();

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Trận đấu không tồn tại hoặc chưa bắt đầu.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Trận đấu đã kết thúc. Người thắng: {room.GameState.WinnerUsername}.";
                }
                else if (room.GameState.IsWaitingForCardChoice)
                {
                    failMessage = "Đang chờ người chơi chọn mục tiêu thẻ. Hãy hoàn tất chọn thẻ trước";
                }
                else if (room.GameState.IsWaitingForPropertySale)
                {
                    failMessage = $"Äang chá» {room.GameState.PendingSalePlayerUsername} bÃ¡n tÃ i sáº£n Ä‘á»ƒ tráº£ {room.GameState.PendingDebtReason}.";
                }
                else
                {
                    List<string> actionMessages = new List<string>();
                    bool grantExtraRoll = false;
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
                    else if (!player.IsOnIsland && player.SkipTurnsLeft > 0)
                    {
                        player.SkipTurnsLeft--;
                        string skipMessage = player.SkipReason == "WORLD_TOUR"
                            ? $"{player.Username} đang chờ chuyến bay Du Lịch Thế Giới và bỏ lượt này."
                            : $"{player.Username} bị đóng băng giao dịch và bỏ lượt này.";

                        if (player.SkipTurnsLeft == 0)
                            player.SkipReason = "";

                        room.GameState.LastDice1 = 0;
                        room.GameState.LastDice2 = 0;
                        room.GameState.LastDiceTotal = 0;
                        room.GameState.LastMovedPlayerIndex = player.PlayerIndex;
                        room.GameState.LastMoveFromPosition = player.Position;
                        room.GameState.LastMoveToPosition = player.Position;
                        room.GameState.LastFinalPosition = player.Position;
                        actionMessages.Add(skipMessage);
                    }
                    else
                    {
                        int boardSize = BoardDatabase.Squares.Count;
                        int dice1 = RollDie();
                        int dice2 = room.GameState.ForceDoubleThisTurn ? dice1 : RollDie();
                        int diceTotal = dice1 + dice2;
                        int oldPosition = player.Position;

                        if (room.GameState.ForceDoubleThisTurn)
                        {
                            room.GameState.ForceDoubleThisTurn = false;
                            actionMessages.Add($"{player.Username} dùng Xúc Xắc Ma Thuật và đổ đôi {dice1}.");
                        }

                        if (player.IsOnIsland || player.JailTurnsLeft > 0)
                        {
                            bool canLeaveIsland = dice1 == dice2;

                            actionMessages.Add($"{player.Username} ở Đảo Hoang và đổ {dice1} + {dice2} = {diceTotal}.");

                            if (canLeaveIsland)
                            {
                                player.IsOnIsland = false;
                                player.JailTurnsLeft = 0;
                                actionMessages.Add($"{player.Username} lắc đôi và thoát Đảo Hoang.");
                                GameEngine.MovePlayerByDiceUnsafe(room.GameState, player, oldPosition, dice1, dice2, actionMessages, cardDrawEvents);
                            }
                            else if (player.JailTurnsLeft > 1)
                            {
                                player.JailTurnsLeft--;
                                room.GameState.LastDice1 = dice1;
                                room.GameState.LastDice2 = dice2;
                                room.GameState.LastDiceTotal = diceTotal;
                                room.GameState.LastMovedPlayerIndex = player.PlayerIndex;
                                room.GameState.LastMoveFromPosition = oldPosition;
                                room.GameState.LastMoveToPosition = oldPosition;
                                room.GameState.LastFinalPosition = oldPosition;
                                actionMessages.Add($"{player.Username} chưa lắc đôi, còn {player.JailTurnsLeft} lượt trên Đảo Hoang.");
                            }
                            else
                            {
                                const long islandExitFee = 200000;
                                player.Money -= islandExitFee;
                                player.IsOnIsland = false;
                                player.JailTurnsLeft = 0;
                                actionMessages.Add($"{player.Username} trả {islandExitFee:N0} và rời Đảo Hoang.");
                                GameEngine.MovePlayerByDiceUnsafe(room.GameState, player, oldPosition, dice1, dice2, actionMessages, cardDrawEvents);
                            }
                        }
                        else
                        {
                            GameEngine.MovePlayerByDiceUnsafe(room.GameState, player, oldPosition, dice1, dice2, actionMessages, cardDrawEvents);

                            bool isDouble = dice1 == dice2;

                            if (room.GameState.IsWaitingForPropertySale)
                            {
                                // Đang chờ bán tài sản trả nợ: không xử lý đổ đôi, không cho tung lại.
                                player.ConsecutiveDoubles = 0;
                            }
                            else if (isDouble && !player.IsOnIsland)
                            {
                                player.ConsecutiveDoubles++;
                                if (player.ConsecutiveDoubles >= 3)
                                {
                                    player.ConsecutiveDoubles = 0;
                                    GameEngine.SendPlayerToIslandUnsafe(player);
                                    room.GameState.LastFinalPosition = player.Position;
                                    actionMessages.Add($"{player.Username} đổ đôi 3 lần liên tiếp và bị đưa thẳng vào Đảo Hoang!");
                                }
                                else
                                {
                                    grantExtraRoll = true;
                                    actionMessages.Add($"{player.Username} đổ đôi ({dice1}) — được tung thêm một lần.");
                                }
                            }
                            else
                            {
                                player.ConsecutiveDoubles = 0;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(failMessage))
                    {
                        room.GameState.HasRolledThisTurn = !grantExtraRoll;
                        GameEngine.ResolveBankruptcyAndWinnerUnsafe(room.GameState, player, actionMessages);
                        room.GameState.LastActionMessage = string.Join(" ", actionMessages);
                        GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                        broadcastMessage = room.GameState.LastActionMessage;
                        shouldBroadcast = true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            if (shouldBroadcast)
            {
                foreach (CardDrawEvent cardDrawEvent in cardDrawEvents)
                {
                    await NetworkSender.BroadcastCardDrawnAsync(roomId, cardDrawEvent);
                }

                await NetworkSender.BroadcastGameStateAsync(roomId, broadcastMessage);
            }
        }
        public static async Task HandleEndTurnAsync(ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, "Bạn chưa ở trong trận nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Trận đấu không tồn tại hoặc chưa bắt đầu.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Trận đấu đã kết thúc. Người thắng: {room.GameState.WinnerUsername}.";
                }
                else if (room.GameState.IsWaitingForCardChoice)
                {
                    failMessage = "Đang chờ người chơi chọn mục tiêu thẻ. Hãy hoàn tất chọn thẻ trước.";
                }
                else if (room.GameState.IsWaitingForPropertySale)
                {
                    failMessage = $"Äang chá» {room.GameState.PendingSalePlayerUsername} bÃ¡n tÃ i sáº£n Ä‘á»ƒ tráº£ {room.GameState.PendingDebtReason}.";
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
                        GameEngine.StartNextTurnUnsafe(room.GameState, out GamePlayerState? nextPlayer);

                        GameEngine.ResetTurnTimerUnsafe(room.GameState);
                        string nextUsername = nextPlayer?.Username ?? "Người chơi";
                        room.GameState.LastActionMessage =
                            $"{player.Username} kết thúc lượt. Đến lượt {nextUsername}.";
                        GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                        broadcastMessage = room.GameState.LastActionMessage;
                        shouldBroadcast = true;

                        Console.WriteLine(
                            $"[END_TURN] Room={roomId}, From={player.Username}, " +
                            $"Next={nextUsername}, Turn={room.GameState.TurnNumber}"
                        );
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            if (shouldBroadcast)
            {
                await NetworkSender.BroadcastGameStateAsync(roomId, broadcastMessage);
            }
        }
        public static async Task HandleBuyPropertyAsync(ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;
            if (string.IsNullOrWhiteSpace(roomId))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, "Bạn chưa ở trong trận nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Trận đấu không tồn tại hoặc chưa bắt đầu.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Trận đấu đã kết thúc. Người thắng: {room.GameState.WinnerUsername}.";
                }
                else if (room.GameState.IsWaitingForCardChoice)
                {
                    failMessage = "Đang chờ người chơi chọn mục tiêu thẻ.";
                }
                else if (room.GameState.IsWaitingForPropertySale)
                {
                    failMessage = $"Đang chờ {room.GameState.PendingSalePlayerUsername} bán tài sản.";
                }
                else
                {
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected && !p.IsBankrupt
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm thấy người chơi trong trận.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chưa đến lượt của bạn.";
                    }
                    else if (!room.GameState.HasRolledThisTurn)
                    {
                        failMessage = "Bạn cần đổ xúc xắc trước khi mua đất.";
                    }
                    else if (!room.GameState.Properties.TryGetValue(player.Position, out GamePropertyState property))
                    {
                        failMessage = "Không tìm thấy thông tin ô hiện tại.";
                    }
                    else if (!GameEngine.TryBuyPropertyUnsafe(room.GameState, player, property, out failMessage))
                    {
                        // failMessage set by GameEngine
                    }
                    else
                    {
                        room.GameState.LastActionMessage = $"{player.Username} đã mua {property.Name} với giá {property.BuyPrice:N0}.";
                        GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                        broadcastMessage = room.GameState.LastActionMessage;
                        shouldBroadcast = true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            if (shouldBroadcast)
            {
                await NetworkSender.BroadcastGameStateAsync(roomId, broadcastMessage);
            }
        }
        public static async Task HandleBuildPropertyAsync(JObject packet, ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;
            JObject payload = PacketHelper.GetPayloadObject(packet);
            int positionIndex = payload["PositionIndex"]?.Value<int>() ?? -1;

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, "Bạn chưa ở trong trận nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Trận đấu không tồn tại hoặc chưa bắt đầu.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Trận đấu đã kết thúc. Người thắng: {room.GameState.WinnerUsername}.";
                }
                else if (room.GameState.IsWaitingForCardChoice)
                {
                    failMessage = "Đang chờ chọn mục tiêu thẻ.";
                }
                else if (room.GameState.IsWaitingForPropertySale)
                {
                    failMessage = $"Đang chờ {room.GameState.PendingSalePlayerUsername} bán tài sản.";
                }
                else
                {
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected && !p.IsBankrupt
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm thấy người chơi trong trận.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chưa đến lượt của bạn.";
                    }
                    else if (!room.GameState.Properties.TryGetValue(positionIndex, out GamePropertyState property))
                    {
                        failMessage = "Không tìm thấy thông tin ô đất.";
                    }
                    else if (!GameEngine.TryBuildPropertyUnsafe(room.GameState, player, property, out failMessage))
                    {
                        // failMessage set by GameEngine
                    }
                    else
                    {
                        long buildCost = GameEngine.GetBuildCostUnsafe(property);
                        string levelDesc = GameEngine.DescribePropertyLevelUnsafe(property);
                        room.GameState.LastActionMessage = $"{player.Username} đã xây {levelDesc} tại {property.Name} với giá {buildCost:N0}.";
                        GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                        broadcastMessage = room.GameState.LastActionMessage;
                        shouldBroadcast = true;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            if (shouldBroadcast)
            {
                await NetworkSender.BroadcastGameStateAsync(roomId, broadcastMessage);
            }
        }
        public static async Task HandleResumeGameAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = PacketHelper.GetPayloadObject(packet);
            string username = payload["Username"]?.ToString() ?? connection.Username;
            Room resumeRoomSnapshot = null;
            string roomId = "";
            string failMessage = "";

            if (string.IsNullOrWhiteSpace(username))
            {
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "RESUME_GAME_NONE",
                    Payload = new { Message = "Không có phiên người chơi để khôi phục." }
                });
                return;
            }

            lock (ServerState.Lock)
            {
                foreach (Room room in ServerState.Rooms.Values)
                {
                    if (!room.IsStarted || room.GameState == null || room.GameState.IsFinished)
                        continue;

                    GamePlayerState disconnectedPlayer = room.GameState.Players.FirstOrDefault(
                        p => p.Username == username && !p.IsBot && !p.IsConnected
                    );

                    if (disconnectedPlayer == null)
                        continue;

                    disconnectedPlayer.IsConnected = true;
                    connection.Username = username;
                    connection.CurrentRoomId = room.RoomId;
                    room.GameState.LastActionMessage = $"{username} đã kết nối lại trận.";
                    GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                    resumeRoomSnapshot = room;
                    roomId = room.RoomId;
                    break;
                }

                if (resumeRoomSnapshot == null)
                    failMessage = "Không có trận đang chờ kết nối lại.";
            }

            if (resumeRoomSnapshot == null)
            {
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
                {
                    Type = "RESUME_GAME_NONE",
                    Payload = new { Message = failMessage }
                });
                return;
            }

            resumeRoomSnapshot.GameState.ServerUtcTicks = DateTime.UtcNow.Ticks;

            await NetworkSender.SendJsonPacketAsync(connection.Stream, new
            {
                Type = "GAME_STARTING",
                Payload = new
                {
                    RoomId = resumeRoomSnapshot.RoomId,
                    MapName = resumeRoomSnapshot.MapName,
                    Players = resumeRoomSnapshot.Players,
                    GameState = resumeRoomSnapshot.GameState
                }
            });

            await NetworkSender.BroadcastGameStateAsync(roomId, $"{username} đã kết nối lại trận.");
        }
        public static async Task HandleLeaveRoomAsync(ClientConnection connection, bool sendLeaveSuccess)
        {
            string roomId;
            string username;
            bool shouldCloseRoom = false;
            bool shouldBroadcastGameState = false;
            string gameStateMessage = "";
            List<ClientConnection> roomClosedTargets = null;

            lock (ServerState.Lock)
            {
                roomId = connection.CurrentRoomId;
                username = connection.Username;

                if (string.IsNullOrWhiteSpace(roomId))
                {
                    return;
                }

                if (!ServerState.Rooms.TryGetValue(roomId, out Room room))
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

                    if (connectedHumans.Count <= 1)
                    {
                        room.GameState.IsFinished = true;
                        if (string.IsNullOrWhiteSpace(room.GameState.EndReason))
                            room.GameState.EndReason = "Đối thủ ngắt kết nối";
                        room.GameState.HasRolledThisTurn = true;
                        room.GameState.TurnEndsAtUtcTicks = 0;

                        if (connectedHumans.Count == 1)
                        {
                            room.GameState.WinnerUsername = connectedHumans[0].Username;
                            gameStateMessage += $" {connectedHumans[0].Username} thắng trận.";
                        }
                        else
                        {
                            var winner = room.GameState.Players
                                .Where(p => p.IsConnected && !p.IsBankrupt)
                                .OrderByDescending(p => p.Money)
                                .FirstOrDefault();
                            
                            room.GameState.WinnerUsername = winner?.Username ?? "";
                            gameStateMessage += $" Trận đấu kết thúc do không còn người chơi thật.";
                        }
                    }
                    else if (gamePlayer != null &&
                        room.GameState.CurrentTurnPlayerIndex == gamePlayer.PlayerIndex &&
                        !room.GameState.IsFinished)
                    {
                        GameEngine.StartNextTurnUnsafe(room.GameState, out GamePlayerState? nextPlayer);

                        GameEngine.ResetTurnTimerUnsafe(room.GameState);
                        string nextUsername = nextPlayer?.Username ?? "Người chơi";
                        gameStateMessage += $" Đến lượt {nextUsername}.";
                    }

                    room.GameState.LastActionMessage = gameStateMessage;
                    GameEngine.AddGameLogUnsafe(room.GameState, gameStateMessage);
                    shouldBroadcastGameState = true;
                }
                else
                {
                    room.Players.RemoveAll(p => p.Username == username && !p.IsBot);
                    connection.CurrentRoomId = "";

                    if (isHostLeaving || room.Players.Count == 0)
                    {
                        shouldCloseRoom = true;

                        ServerState.Rooms.Remove(roomId);

                        roomClosedTargets = ServerState.Clients.Values
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
                await NetworkSender.SendJsonPacketAsync(connection.Stream, new
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
                await NetworkSender.BroadcastGameStateAsync(roomId, gameStateMessage);
                Console.WriteLine($"[GAME] {username} rời trận {roomId}.");
                return;
            }

            if (shouldCloseRoom)
            {
                if (roomClosedTargets != null)
                {
                    foreach (ClientConnection target in roomClosedTargets)
                    {
                        await NetworkSender.SendJsonPacketAsync(target.Stream, new
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
                await NetworkSender.BroadcastRoomUpdateAsync(roomId);

                Console.WriteLine($"[ROOM] {username} rời phòng {roomId}.");
            }
        }
    }
}
