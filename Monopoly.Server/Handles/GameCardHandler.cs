using Monopoly.Server.GameLogic;
using Monopoly.Server.Models;
using Monopoly.Server.Models.Events;
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
        public static async Task HandleUseCardAsync(JObject packet, ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;
            JObject payload = PacketHelper.GetPayloadObject(packet);
            string cardId = payload["CardId"]?.ToString() ??
                payload["EffectCode"]?.ToString() ??
                payload["CardEffectCode"]?.ToString() ?? "";
            string effectCode = GameEngine.NormalizeCardEffectCode(cardId);
            int? targetPosition = payload["PositionIndex"]?.Value<int?>();

            if (string.IsNullOrWhiteSpace(roomId))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, "Bạn chưa ở trong trận đấu nào.");
                return;
            }

            string failMessage = "";
            string broadcastMessage = "";
            bool shouldBroadcast = false;
            bool shouldSendChoiceRequest = false;
            List<int> validTargets = new List<int>();
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
                        failMessage = $"Chưa đến lượt của bạn. Hiện tại là lượt của {room.GameState.CurrentTurnUsername}.";
                    }
                    else if (string.IsNullOrWhiteSpace(effectCode))
                    {
                        failMessage = "CardId không hợp lệ.";
                    }
                    else if (room.GameState.IsWaitingForCardChoice)
                    {
                        failMessage = "Đang có thẻ khác chờ chọn mục tiêu.";
                    }
                    else if (room.GameState.IsWaitingForPropertySale)
                    {
                        failMessage = $"Đang chờ {room.GameState.PendingSalePlayerUsername} bán tài sản để trả {room.GameState.PendingDebtReason}.";
                    }
                    else if (!GameEngine.PlayerHasHeldCardUnsafe(player, effectCode))
                    {
                        failMessage = "Bạn không sở hữu thẻ này hoặc thẻ đã được dùng.";
                    }
                    else if (GameEngine.RequiresCardTarget(effectCode) && !targetPosition.HasValue)
                    {
                        validTargets = GameEngine.BuildCardTargetPositionsUnsafe(room.GameState, player, effectCode);

                        if (validTargets.Count == 0)
                        {
                            failMessage = "Không có mục tiêu hợp lệ cho thẻ này.";
                        }
                        else
                        {
                            room.GameState.IsWaitingForCardChoice = true;
                            room.GameState.PendingCardEffectCode = effectCode;
                            room.GameState.PendingCardPlayerUsername = player.Username;
                            room.GameState.PendingCardTargetPositions.Clear();
                            room.GameState.PendingCardTargetPositions.AddRange(validTargets);
                            room.GameState.LastActionMessage = $"{player.Username} đang chọn mục tiêu cho thẻ {effectCode}.";
                            GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                            broadcastMessage = room.GameState.LastActionMessage;
                            shouldBroadcast = true;
                            shouldSendChoiceRequest = true;
                        }
                    }
                    else
                    {
                        List<string> actionMessages = new List<string>();

                        if (GameEngine.TryApplyHeldCardEffectUnsafe(
                                room.GameState,
                                player,
                                effectCode,
                                targetPosition,
                                actionMessages,
                                cardDrawEvents,
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

            if (shouldSendChoiceRequest)
            {
                await NetworkSender.SendCardChoiceRequestAsync(connection, effectCode, validTargets);
            }

            foreach (CardDrawEvent cardDrawEvent in cardDrawEvents)
            {
                await NetworkSender.BroadcastCardDrawnAsync(roomId, cardDrawEvent);
            }

            if (shouldBroadcast)
            {
                await NetworkSender.BroadcastGameStateAsync(roomId, broadcastMessage);
            }
        }

        public static async Task HandleCardChoiceMadeAsync(JObject packet, ClientConnection connection)
        {
            string roomId = connection.CurrentRoomId;
            JObject payload = PacketHelper.GetPayloadObject(packet);
            string effectCode = GameEngine.NormalizeCardEffectCode(payload["EffectCode"]?.ToString() ?? payload["CardId"]?.ToString() ?? "");
            int positionIndex = payload["PositionIndex"]?.Value<int>() ?? -1;
            bool cancel = payload["Cancel"]?.Value<bool>() ?? false;

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
                else if (!room.GameState.IsWaitingForCardChoice)
                {
                    failMessage = "Không có thẻ nào đang chờ chọn mục tiêu.";
                }
                else if (!string.Equals(room.GameState.PendingCardPlayerUsername, connection.Username, StringComparison.OrdinalIgnoreCase))
                {
                    failMessage = "Không phải lượt chọn mục tiêu thẻ của bạn.";
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
                    else if (cancel)
                    {
                        GameEngine.ClearPendingCardChoiceUnsafe(room.GameState);
                        room.GameState.LastActionMessage = $"{player.Username} hủy chọn mục tiêu thẻ.";
                        GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                        broadcastMessage = room.GameState.LastActionMessage;
                        shouldBroadcast = true;
                    }
                    else
                    {
                        string pendingEffectCode = room.GameState.PendingCardEffectCode;

                        if (!string.IsNullOrWhiteSpace(effectCode) &&
                            !string.Equals(effectCode, pendingEffectCode, StringComparison.OrdinalIgnoreCase))
                        {
                            failMessage = "Lựa chọn không khớp với thẻ đang chọn.";
                        }
                        else if (!room.GameState.PendingCardTargetPositions.Contains(positionIndex))
                        {
                            failMessage = "Mục tiêu không hợp lệ cho thẻ này.";
                        }
                        else
                        {
                            List<string> actionMessages = new List<string>();

                            if (GameEngine.TryApplyHeldCardEffectUnsafe(
                                    room.GameState,
                                    player,
                                    pendingEffectCode,
                                    positionIndex,
                                    actionMessages,
                                    cardDrawEvents,
                                    out failMessage))
                            {
                                GameEngine.ClearPendingCardChoiceUnsafe(room.GameState);
                                GameEngine.ResolveBankruptcyAndWinnerUnsafe(room.GameState, player, actionMessages);
                                room.GameState.LastActionMessage = string.Join(" ", actionMessages);
                                GameEngine.AddGameLogUnsafe(room.GameState, room.GameState.LastActionMessage);
                                broadcastMessage = room.GameState.LastActionMessage;
                                shouldBroadcast = true;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(failMessage))
            {
                await NetworkSender.SendGameActionFailedAsync(connection, failMessage);
                return;
            }

            foreach (CardDrawEvent cardDrawEvent in cardDrawEvents)
            {
                await NetworkSender.BroadcastCardDrawnAsync(roomId, cardDrawEvent);
            }

            if (shouldBroadcast)
            {
                await NetworkSender.BroadcastGameStateAsync(roomId, broadcastMessage);
            }
        }
    }
}
