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
    public static partial class GameHandler
    {
        public static async Task HandleSellPropertyForDebtAsync(JObject packet, ClientConnection connection)
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
            GamePlayerState player = null;

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
                else if (!room.GameState.IsWaitingForPropertySale)
                {
                    failMessage = "Không có khoản nợ nào đang chờ bán tài sản.";
                }
                else if (!string.Equals(room.GameState.PendingSalePlayerUsername, connection.Username, StringComparison.OrdinalIgnoreCase))
                {
                    failMessage = "Không phải lượt bán tài sản của bạn.";
                }
                else
                {
                    player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected && !p.IsBankrupt
                    );

                    if (player == null)
                    {
                        failMessage = "Không tìm thấy người chơi trong trận.";
                    }
                    else
                    {
                        List<string> actionMessages = new List<string>();

                        if (GameEngine.TrySellPropertyForDebtUnsafe(
                                room.GameState,
                                player,
                                positionIndex,
                                actionMessages,
                                out failMessage))
                        {
                            GameEngine.ResolveBankruptcyAndWinnerUnsafe(room.GameState, player, actionMessages);
                            room.GameState.LastActionMessage = string.Join(" ", actionMessages);
                            GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                            broadcastMessage = room.GameState.LastActionMessage;
                            shouldBroadcast = true;
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
    }
}
