using Monopoly.Server.Models;
using Monopoly.Server.Models.Events;
using Monopoly.Server.Models.State;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Monopoly.Server.Models.State;
using Monopoly.Server.GameLogic;


namespace Monopoly.Server.Network
{
    public class NetworkSender
    {
        public static async Task SendJsonPacketAsync(NetworkStream stream, object responseObject)
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
                // Client dã dóng k?t n?i, b? qua.
            }
        }
        public static async Task BroadcastToRoomAsync(string roomId, object packet)
        {
            List<ClientConnection> targets;

            lock (ServerState.Lock)
            {
                targets = ServerState.Clients.Values
                    .Where(c => c.CurrentRoomId == roomId)
                    .ToList();
            }

            foreach (ClientConnection target in targets)
            {
                await SendJsonPacketAsync(target.Stream, packet);
            }
        }
        public static async Task BroadcastRoomUpdateAsync(string roomId)
        {
            Room roomSnapshot;

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room))
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
        public static async Task BroadcastGameStartingAsync(Room room)
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
        public static async Task BroadcastGameStateAsync(string roomId, string message)
        {
            GameState gameState;
            bool shouldBroadcastGameOver = false;
            List<GameOverRankingResult> rankings = new List<GameOverRankingResult>();
            string matchId = "";

            lock (ServerState.Lock)
            {
                if (!ServerState.Rooms.TryGetValue(roomId, out Room room) || room.GameState == null)
                {
                    return;
                }

                gameState = room.GameState;
                gameState.ServerUtcTicks = DateTime.UtcNow.Ticks;

                if (gameState.IsFinished && !gameState.GameOverBroadcasted)
                {
                    if (string.IsNullOrWhiteSpace(gameState.MatchId))
                        gameState.MatchId = Guid.NewGuid().ToString("N");

                    rankings = GameEngine.BuildGameOverRankingsUnsafe(gameState);
                    matchId = gameState.MatchId;
                    gameState.GameOverBroadcasted = true;
                    shouldBroadcastGameOver = true;
                }
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

            if (shouldBroadcastGameOver)
            {
                await BroadcastGameOverAsync(roomId, matchId, rankings);
            }
        }
        public static async Task BroadcastGameOverAsync(string roomId, string matchId, List<GameOverRankingResult> rankings)
        {
            await BroadcastToRoomAsync(roomId, new
            {
                Type = "GAME_OVER",
                Payload = new
                {
                    MatchId = matchId,
                    Rankings = rankings
                }
            });

            Console.WriteLine($"[GAME_OVER_PACKET] Room={roomId}, MatchId={matchId}");

            await GameEngine.PersistMatchResultsAsync(matchId, rankings);
        }
        public static async Task BroadcastCardDrawnAsync(string roomId, CardDrawEvent cardDrawEvent)
        {
            if (cardDrawEvent == null)
            {
                return;
            }

            await BroadcastToRoomAsync(roomId, new
            {
                Type = "CARD_DRAWN",
                Payload = new
                {
                    DrawnByUsername = cardDrawEvent.DrawnByUsername,
                    CardId = cardDrawEvent.CardId,
                    CardName = cardDrawEvent.CardName,
                    CardType = cardDrawEvent.CardType,
                    DetailEffect = cardDrawEvent.DetailEffect
                }
            });
        }
        public static async Task SendGameActionFailedAsync(ClientConnection connection, string message)
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
        public static async Task SendRawStringAsync(NetworkStream stream, string message)
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
                // Client dã dóng k?t n?i, b? qua.
            }
        }
    }
}


