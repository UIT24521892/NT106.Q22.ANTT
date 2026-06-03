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
                await NetworkSender.SendGameActionFailedAsync(connection, "Ban chua o trong tran nao.");
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
                    failMessage = "Tran dau khong ton tai hoac chua bat dau.";
                }
                else if (room.GameState.IsFinished)
                {
                    failMessage = $"Tran dau da ket thuc. Nguoi thang: {room.GameState.WinnerUsername}.";
                }
                else
                {
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected && !p.IsBankrupt
                    );

                    if (player == null)
                    {
                        failMessage = "Khong tim thay nguoi choi trong tran.";
                    }
                    else if (player.PlayerIndex != room.GameState.CurrentTurnPlayerIndex)
                    {
                        failMessage = $"Chua den luot cua ban. Hien tai la luot cua {room.GameState.CurrentTurnUsername}.";
                    }
                    else if (string.IsNullOrWhiteSpace(effectCode))
                    {
                        failMessage = "CardId khong hop le.";
                    }
                    else if (room.GameState.IsWaitingForCardChoice)
                    {
                        failMessage = "Dang co the khac cho chon muc tieu.";
                    }
                    else if (room.GameState.IsWaitingForPropertySale)
                    {
                        failMessage = $"Đang chờ {room.GameState.PendingSalePlayerUsername} bán tài sản để trả {room.GameState.PendingDebtReason}.";
                    }
                    else if (!GameEngine.PlayerHasHeldCardUnsafe(player, effectCode))
                    {
                        failMessage = "Ban khong so huu the nay hoac the da duoc dung.";
                    }
                    else if (GameEngine.RequiresCardTarget(effectCode) && !targetPosition.HasValue)
                    {
                        validTargets = GameEngine.BuildCardTargetPositionsUnsafe(room.GameState, player, effectCode);

                        if (validTargets.Count == 0)
                        {
                            failMessage = "Khong co muc tieu hop le cho the nay.";
                        }
                        else
                        {
                            room.GameState.IsWaitingForCardChoice = true;
                            room.GameState.PendingCardEffectCode = effectCode;
                            room.GameState.PendingCardPlayerUsername = player.Username;
                            room.GameState.PendingCardTargetPositions.Clear();
                            room.GameState.PendingCardTargetPositions.AddRange(validTargets);
                            room.GameState.LastActionMessage = $"{player.Username} dang chon muc tieu cho the {effectCode}.";
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
                await NetworkSender.SendGameActionFailedAsync(connection, "Ban chua o trong tran nao.");
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
                    failMessage = "Tran dau khong ton tai hoac chua bat dau.";
                }
                else if (!room.GameState.IsWaitingForCardChoice)
                {
                    failMessage = "Khong co the nao dang cho chon muc tieu.";
                }
                else if (!string.Equals(room.GameState.PendingCardPlayerUsername, connection.Username, StringComparison.OrdinalIgnoreCase))
                {
                    failMessage = "Khong phai luot chon muc tieu the cua ban.";
                }
                else
                {
                    GamePlayerState player = room.GameState.Players.FirstOrDefault(
                        p => p.Username == connection.Username && !p.IsBot && p.IsConnected && !p.IsBankrupt
                    );

                    if (player == null)
                    {
                        failMessage = "Khong tim thay nguoi choi trong tran.";
                    }
                    else if (cancel)
                    {
                        GameEngine.ClearPendingCardChoiceUnsafe(room.GameState);
                        room.GameState.LastActionMessage = $"{player.Username} huy chon muc tieu the.";
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
                            failMessage = "Lua chon khong khop voi the dang cho.";
                        }
                        else if (!room.GameState.PendingCardTargetPositions.Contains(positionIndex))
                        {
                            failMessage = "Muc tieu khong hop le cho the nay.";
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
