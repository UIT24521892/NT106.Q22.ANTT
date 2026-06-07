using Monopoly.Server.Models.State;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Monopoly.Server.Network;
using Monopoly.Server.GameLogic.Bots;

namespace Monopoly.Server.GameLogic
{
    public static class TurnTimer
    {
        public static async Task RunTurnTimerLoopAsync()
        {
            while (true)
            {
                try
                {
                    await Task.Delay(1000);

                    List<(string RoomId, string Message)> expiredTurns = new List<(string RoomId, string Message)>();
                    List<(Room room, GamePlayerState bot)> botsToPlay = new List<(Room, GamePlayerState)>();

                    lock (ServerState.Lock)
                    {
                        long nowTicks = DateTime.UtcNow.Ticks;

                        foreach (Room room in ServerState.Rooms.Values)
                        {
                            if (!room.IsStarted || room.GameState == null || room.GameState.IsFinished)
                            {
                                continue;
                            }

                            if (room.GameState.IsPaused)
                            {
                                continue;
                            }

                            if (room.GameState.MatchEndsAtUtcTicks > 0 &&
                                nowTicks >= room.GameState.MatchEndsAtUtcTicks)
                            {
                                List<string> timeoutMessages = new List<string>();
                                GameEngine.FinishMatchByTimeUnsafe(room.GameState, timeoutMessages);
                                string timeoutMessage = string.Join(" ", timeoutMessages);
                                room.GameState.LastActionMessage = timeoutMessage;
                                GameEngine.AddGameLogUnsafe(room.GameState, timeoutMessage);
                                expiredTurns.Add((room.RoomId, timeoutMessage));
                                continue;
                            }

                            GamePlayerState currentPlayer = room.GameState.Players.FirstOrDefault(
                                p => p.PlayerIndex == room.GameState.CurrentTurnPlayerIndex
                            );

                            if (room.GameState.IsWaitingForPropertySale || room.GameState.IsWaitingForCardChoice)
                            {
                                continue;
                            }

                            // Bắt sự kiện BOT
                            if (currentPlayer != null && currentPlayer.IsBot && !room.GameState.IsBotPlaying)
                            {
                                room.GameState.IsBotPlaying = true;
                                botsToPlay.Add((room, currentPlayer));
                            }

                            // Xử lý Timeout
                            if (room.GameState.TurnEndsAtUtcTicks > 0 && nowTicks >= room.GameState.TurnEndsAtUtcTicks)
                            {
                                GameEngine.StartNextTurnUnsafe(room.GameState, out GamePlayerState? nextPlayer);
                                room.GameState.IsBotPlaying = false; // Reset cờ bot
                                GameEngine.ResetTurnTimerUnsafe(room.GameState);

                                string currentUsername = currentPlayer?.Username ?? "Nguoi choi";
                                string nextUsername = nextPlayer?.Username ?? "Nguoi choi";
                                room.GameState.LastActionMessage = $"{currentUsername} het thoi gian luot. Den luot {nextUsername}.";
                                GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);

                                expiredTurns.Add((room.RoomId, room.GameState.LastActionMessage));

                                Console.WriteLine($"[TURN_TIMEOUT] Room={room.RoomId}, From={currentUsername}, Next={nextUsername}, Turn={room.GameState.TurnNumber}");
                            }
                        }
                    }

                    foreach (var pair in expiredTurns)
                    {
                        await NetworkSender.BroadcastGameStateAsync(pair.RoomId, pair.Message);
                    }

                    // Kích hoạt bot (không block loop)
                    foreach (var botInfo in botsToPlay)
                    {
                        _ = Task.Run(async () => {
                            try {
                                await BotAIController.PlayBotTurnAsync(botInfo.room, botInfo.bot);
                            } 
                            catch (Exception ex) {
                                Console.WriteLine($"[BOT_ERROR] {ex.Message}");
                            }
                            finally {
                                lock (ServerState.Lock) {
                                    if (botInfo.room.GameState != null) {
                                        botInfo.room.GameState.IsBotPlaying = false;
                                    }
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TURN_TIMER_ERROR] {ex.Message}");
                }
            }
        }
    }
}
