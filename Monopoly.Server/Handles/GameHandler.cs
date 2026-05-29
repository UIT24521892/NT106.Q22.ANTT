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
    public static class GameHandler
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
                await NetworkSender.SendGameActionFailedAsync(connection, "B?n chua ? trong tr?n nào.");
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
                    failMessage = "Tr?n d?u không t?n t?i ho?c chua b?t d?u.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Tr?n d?u dã k?t thúc. Ngu?i th?ng: {room.GameState.WinnerUsername}.";
                }
                else
                {
                    List<string> actionMessages = new List<string>();
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm th?y ngu?i choi trong tr?n.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chua d?n lu?t c?a b?n. Hi?n t?i là lu?t c?a {room.GameState.CurrentTurnUsername}.";
                    }
                    else if (room.GameState.HasRolledThisTurn)
                    {
                        failMessage = "B?n dã d? xúc x?c trong lu?t này. Hãy k?t thúc lu?t.";
                    }
                    else if (!player.IsOnIsland && player.SkipTurnsLeft > 0)
                    {
                        player.SkipTurnsLeft--;
                        string skipMessage = player.SkipReason == "WORLD_TOUR"
                            ? $"{player.Username} dang ch? chuy?n bay Du L?ch Th? Gi?i và b? lu?t này."
                            : $"{player.Username} b? dóng bang giao d?ch và b? lu?t này.";

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
                        int dice2 = RollDie();
                        int diceTotal = dice1 + dice2;
                        int oldPosition = player.Position;

                        if (player.IsOnIsland || player.JailTurnsLeft > 0)
                        {
                            bool canLeaveIsland = dice1 == dice2;

                            actionMessages.Add($"{player.Username} ? Ð?o Hoang và d? {dice1} + {dice2} = {diceTotal}.");

                            if (canLeaveIsland)
                            {
                                player.IsOnIsland = false;
                                player.JailTurnsLeft = 0;
                                actionMessages.Add($"{player.Username} l?c dôi và thoát Ð?o Hoang.");
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
                                actionMessages.Add($"{player.Username} chua l?c dôi, còn {player.JailTurnsLeft} lu?t trên Ð?o Hoang.");
                            }
                            else
                            {
                                const long islandExitFee = 200000;
                                player.Money -= islandExitFee;
                                player.IsOnIsland = false;
                                player.JailTurnsLeft = 0;
                                actionMessages.Add($"{player.Username} tr? {islandExitFee:N0} d? r?i Ð?o Hoang.");
                                GameEngine.MovePlayerByDiceUnsafe(room.GameState, player, oldPosition, dice1, dice2, actionMessages, cardDrawEvents);
                            }
                        }
                        else
                        {
                            GameEngine.MovePlayerByDiceUnsafe(room.GameState, player, oldPosition, dice1, dice2, actionMessages, cardDrawEvents);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(failMessage))
                    {
                        room.GameState.HasRolledThisTurn = true;
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
                await NetworkSender.SendGameActionFailedAsync(connection, "B?n chua ? trong tr?n nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Tr?n d?u không t?n t?i ho?c chua b?t d?u.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Tr?n d?u dã k?t thúc. Ngu?i th?ng: {room.GameState.WinnerUsername}.";
                }
                else
                {
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm th?y ngu?i choi trong tr?n.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chua d?n lu?t c?a b?n. Hi?n t?i là lu?t c?a {room.GameState.CurrentTurnUsername}.";
                    }
                    else if (!room.GameState.HasRolledThisTurn)
                    {
                        failMessage = "B?n c?n d? xúc x?c tru?c khi k?t thúc lu?t.";
                    }
                    else
                    {
                        GamePlayerState nextPlayer = GameEngine.GetNextTurnPlayerUnsafe(room.GameState);

                        room.GameState.CurrentTurnPlayerIndex = nextPlayer.PlayerIndex;
                        room.GameState.CurrentTurnUsername = nextPlayer.Username;
                        room.GameState.TurnNumber++;
                        room.GameState.HasRolledThisTurn = false;
                        GameEngine.ResetTurnTimerUnsafe(room.GameState);
                        room.GameState.LastActionMessage =
                            $"{player.Username} k?t thúc lu?t. Ð?n lu?t {nextPlayer.Username}.";
                        GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

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
                await NetworkSender.SendGameActionFailedAsync(connection, "B?n chua ? trong tr?n nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Tr?n d?u không t?n t?i ho?c chua b?t d?u.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Tr?n d?u dã k?t thúc. Ngu?i th?ng: {room.GameState.WinnerUsername}.";
                }
                else
                {
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm th?y ngu?i choi trong tr?n.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chua d?n lu?t c?a b?n. Hi?n t?i là lu?t c?a {room.GameState.CurrentTurnUsername}.";
                    }
                    else if (!room.GameState.HasRolledThisTurn)
                    {
                        failMessage = "B?n c?n d? xúc x?c tru?c khi mua d?t.";
                    }
                    else if (!room.GameState.Properties.TryGetValue(player.Position, out GamePropertyState property))
                    {
                        failMessage = "Không tìm th?y thông tin ô hi?n t?i.";
                    }
                    else if (property.Type != "City" && property.Type != "Resort")
                    {
                        failMessage = $"Ô {property.Name} không th? mua.";
                    }
                    else if (property.OwnerPlayerIndex >= 0)
                    {
                        failMessage = $"Ô {property.Name} dã có ch?.";
                    }
                    else if (property.BuyPrice <= 0)
                    {
                        failMessage = $"Ô {property.Name} chua có giá mua h?p l?.";
                    }
                    else if (player.Money < property.BuyPrice)
                    {
                        failMessage = $"B?n không d? ti?n d? mua {property.Name}.";
                    }
                    else
                    {
                        player.Money -= property.BuyPrice;
                        property.OwnerPlayerIndex = player.PlayerIndex;

                        room.GameState.LastActionMessage =
                            $"{player.Username} dã mua {property.Name} v?i giá {property.BuyPrice:N0}.";
                        GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

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
                await NetworkSender.SendGameActionFailedAsync(connection, "B?n chua ? trong tr?n nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room) || !room.IsStarted || room.GameState == null)
                {
                    failMessage = "Tr?n d?u không t?n t?i ho?c chua b?t d?u.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Tr?n d?u dã k?t thúc. Ngu?i th?ng: {room.GameState.WinnerUsername}.";
                }
                else
                {
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected && !p.IsBankrupt
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm th?y ngu?i choi trong tr?n.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chua d?n lu?t c?a b?n. Hi?n t?i là lu?t c?a {room.GameState.CurrentTurnUsername}.";
                    }
                    else if (!room.GameState.Properties.TryGetValue(positionIndex, out GamePropertyState property))
                    {
                        failMessage = "Không tìm th?y thông tin ô d?t.";
                    }
                    else if (property.Type != "City")
                    {
                        failMessage = $"Ô {property.Name} không th? xây nhà.";
                    }
                    else if (property.OwnerPlayerIndex != player.PlayerIndex)
                    {
                        failMessage = $"B?n không s? h?u {property.Name}.";
                    }
                    else if (property.HasHotel)
                    {
                        failMessage = $"{property.Name} dã có khách s?n.";
                    }
                    else
                    {
                        long buildCost = GameEngine.GetBuildCostUnsafe(property);

                        if (buildCost <= 0)
                        {
                            failMessage = $"{property.Name} chua có chi phí nâng c?p h?p l?.";
                        }
                        else if (player.Money < buildCost)
                        {
                            failMessage = $"B?n không d? ti?n d? nâng c?p {property.Name}.";
                        }
                        else
                        {
                            player.Money -= buildCost;

                            if (property.HouseCount >= 3)
                            {
                                property.HouseCount = 3;
                                property.HasHotel = true;
                            }
                            else
                            {
                                property.HouseCount++;
                            }

                            room.GameState.LastActionMessage =
                                $"{player.Username} nâng c?p {property.Name} lên {GameEngine.DescribePropertyLevelUnsafe(property)} v?i giá {buildCost:N0}.";
                            GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                            broadcastMessage = room.GameState.LastActionMessage;
                            shouldBroadcast = true;

                            Console.WriteLine(
                                $"[BUILD_PROPERTY] Room={roomId}, Player={player.Username}, " +
                                $"Property={property.Name}, Position={property.PositionIndex}, " +
                                $"Cost={buildCost}, Level={GameEngine.DescribePropertyLevelUnsafe(property)}, MoneyLeft={player.Money}"
                            );
                        }
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
                    Payload = new { Message = "Không có phiên ngu?i choi d? khôi ph?c." }
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
                    room.GameState.LastActionMessage = $"{username} dã k?t n?i l?i tr?n.";
                    GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                    resumeRoomSnapshot = room;
                    roomId = room.RoomId;
                    break;
                }

                if (resumeRoomSnapshot == null)
                    failMessage = "Không có tr?n dang ch? k?t n?i l?i.";
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

            await NetworkSender.BroadcastGameStateAsync(roomId, $"{username} dã k?t n?i l?i tr?n.");
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

                    gameStateMessage = $"{username} dã m?t k?t n?i/r?i tr?n.";

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
                        gameStateMessage += $" {connectedHumans[0].Username} th?ng tr?n.";
                    }
                    else if (gamePlayer != null &&
                        room.GameState.CurrentTurnPlayerIndex == gamePlayer.PlayerIndex &&
                        !room.GameState.IsFinished)
                    {
                        GamePlayerState nextPlayer = GameEngine.GetNextTurnPlayerUnsafe(room.GameState);

                        room.GameState.CurrentTurnPlayerIndex = nextPlayer.PlayerIndex;
                        room.GameState.CurrentTurnUsername = nextPlayer.Username;
                        room.GameState.TurnNumber++;
                        room.GameState.HasRolledThisTurn = false;
                        GameEngine.ResetTurnTimerUnsafe(room.GameState);
                        gameStateMessage += $" Ð?n lu?t {nextPlayer.Username}.";
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
                        Message = "Ðã r?i phòng."
                    }
                });
            }

            if (shouldBroadcastGameState)
            {
                await NetworkSender.BroadcastGameStateAsync(roomId, gameStateMessage);
                Console.WriteLine($"[GAME] {username} r?i tr?n {roomId}.");
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
                                Message = "Ch? phòng dã r?i di. Phòng dã b? dóng."
                            }
                        });
                    }
                }

                Console.WriteLine($"[ROOM] Phòng {roomId} dã b? dóng.");
            }
            else
            {
                await NetworkSender.BroadcastRoomUpdateAsync(roomId);

                Console.WriteLine($"[ROOM] {username} r?i phòng {roomId}.");
            }
        }
    }
}

