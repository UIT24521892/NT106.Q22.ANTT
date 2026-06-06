using Monopoly.Server.GameLogic;
using Monopoly.Server.Models;
using Monopoly.Server.Models.State;
using Monopoly.Server.Network;
using Monopoly.Server.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Monopoly.Server.Handles
{
    public static class GameControlHandler
    {
        public static async Task HandleRequestPauseAsync(ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;
            string message;
            string failMessage = "";

            lock (ServerState.Lock)
            {
                if (!TryGetActivePlayerUnsafe(connection, out Room room, out GamePlayerState player, out failMessage))
                {
                    message = "";
                }
                else if (room.GameState.IsPaused)
                {
                    failMessage = "Trận đấu đang tạm dừng.";
                    message = "";
                }
                else
                {
                    room.GameState.PauseRequestedBy = player.Username;
                    room.GameState.PauseVotes = new List<string> { player.Username };
                    message = $"{player.Username} đề nghị tạm dừng. Người chơi còn lại cần đồng ý.";
                    TryApplyPauseUnsafe(room.GameState, ref message);
                    room.GameState.LastActionMessage = message;
                    GameEngine.AddGameLogUnsafe(room.GameState, message);
                }
            }

            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            await NetworkSender.BroadcastGameStateAsync(roomId, message);
        }

        public static async Task HandlePauseVoteAsync(JObject packet, ClientConnection connection)
        {
            JObject payload = PacketHelper.GetPayloadObject(packet);
            bool accept = payload["Accept"]?.Value<bool>() ?? true;
            string roomId = connection.CurrentRoomId;
            string message = "";
            string failMessage = "";

            lock (ServerState.Lock)
            {
                if (!TryGetActivePlayerUnsafe(connection, out Room room, out GamePlayerState player, out failMessage))
                    goto Complete;

                if (string.IsNullOrWhiteSpace(room.GameState.PauseRequestedBy) || room.GameState.IsPaused)
                {
                    failMessage = "Không có yêu cầu tạm dừng đang chờ.";
                    goto Complete;
                }

                if (!accept)
                {
                    room.GameState.PauseRequestedBy = "";
                    room.GameState.PauseVotes.Clear();
                    message = $"{player.Username} từ chối tạm dừng.";
                }
                else
                {
                    if (!room.GameState.PauseVotes.Contains(player.Username))
                        room.GameState.PauseVotes.Add(player.Username);

                    message = $"{player.Username} đồng ý tạm dừng.";
                    TryApplyPauseUnsafe(room.GameState, ref message);
                }

                room.GameState.LastActionMessage = message;
                GameEngine.AddGameLogUnsafe(room.GameState, message);
            }

        Complete:
            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            await NetworkSender.BroadcastGameStateAsync(roomId, message);
        }

        public static async Task HandleResumeGameplayAsync(ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;
            string message = "";
            string failMessage = "";

            lock (ServerState.Lock)
            {
                if (!TryGetActivePlayerUnsafe(connection, out Room room, out GamePlayerState player, out failMessage))
                    goto Complete;

                GameState state = room.GameState;
                if (!state.IsPaused)
                {
                    failMessage = "Trận đấu không ở trạng thái tạm dừng.";
                    goto Complete;
                }

                long pausedTicks = Math.Max(0, DateTime.UtcNow.Ticks - state.PauseStartedAtUtcTicks);
                state.MatchEndsAtUtcTicks += pausedTicks;
                if (state.TurnEndsAtUtcTicks > 0)
                    state.TurnEndsAtUtcTicks += pausedTicks;

                state.IsPaused = false;
                if (state.TurnEndsAtUtcTicks <= 0)
                    GameEngine.ResetTurnTimerUnsafe(state);
                state.PauseStartedAtUtcTicks = 0;
                state.PauseRequestedBy = "";
                state.PauseVotes.Clear();
                message = $"{player.Username} tiếp tục trận đấu.";
                state.LastActionMessage = message;
                GameEngine.AddGameLogUnsafe(state, message);
            }

        Complete:
            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            await NetworkSender.BroadcastGameStateAsync(roomId, message);
        }

        public static async Task HandleSurrenderAsync(ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;
            string message = "";
            string failMessage = "";

            lock (ServerState.Lock)
            {
                if (!TryGetActivePlayerUnsafe(connection, out Room room, out GamePlayerState player, out failMessage))
                    goto Complete;

                List<string> messages = new List<string> { $"{player.Username} đã đầu hàng." };
                GameEngine.HandleBankruptcyUnsafe(room.GameState, player, messages);
                GameEngine.ResolveBankruptcyAndWinnerUnsafe(room.GameState, player, messages);
                message = string.Join(" ", messages);
                room.GameState.LastActionMessage = message;
                GameEngine.AddGameLogUnsafe(room.GameState, message);
            }

        Complete:
            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            await NetworkSender.BroadcastGameStateAsync(roomId, message);
        }

        private static bool TryGetActivePlayerUnsafe(
            ClientConnection connection,
            out Room room,
            out GamePlayerState player,
            out string failMessage)
        {
            room = null;
            player = null;
            failMessage = "";

            if (connection == null ||
                string.IsNullOrWhiteSpace(connection.CurrentRoomId) ||
                !ServerState.Rooms.TryGetValue(connection.CurrentRoomId, out room) ||
                !room.IsStarted ||
                room.GameState == null ||
                room.GameState.IsFinished)
            {
                failMessage = "Trận đấu không còn hoạt động.";
                return false;
            }

            player = room.GameState.Players.FirstOrDefault(
                p => p.Username == connection.Username && !p.IsBot && p.IsConnected && !p.IsBankrupt);

            if (player == null)
            {
                failMessage = "Người chơi không hợp lệ cho thao tác này.";
                return false;
            }

            return true;
        }

        private static void TryApplyPauseUnsafe(GameState state, ref string message)
        {
            List<string> requiredVotes = state.Players
                .Where(p => !p.IsBot && !p.IsBankrupt && p.IsConnected)
                .Select(p => p.Username)
                .ToList();

            if (requiredVotes.Count == 0 || requiredVotes.Any(name => !state.PauseVotes.Contains(name)))
                return;

            state.IsPaused = true;
            state.PauseStartedAtUtcTicks = DateTime.UtcNow.Ticks;
            state.IsBotPlaying = false;
            message = "Tất cả người chơi đã đồng ý. Trận đấu tạm dừng.";
        }
    }
}
