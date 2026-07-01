using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Monopoly.Server.Handles;
using Monopoly.Server.Models;
using Monopoly.Server.Models.State;
using Monopoly.Server.Network;

namespace Monopoly.Server.Network
{
    public static class PacketRouter
    {
        public static async Task RoutePacketAsync(string jsonPacket, ClientConnection connection)
        {
            try
            {
                JObject packet = JObject.Parse(jsonPacket);
                string packetType = packet["Type"]?.ToString() ?? "";
                if (IsBlockedWhilePaused(packetType, connection))
                {
                    await NetworkSender.SendGameActionFailedAsync(connection, 
                        "Trận đấu đang tạm dừng.");
                    return;
                }

                switch (packetType)
                {
                    case "LOGIN":
                    case "REGISTER":
                        await AuthHandler.HandleAuthAsync(packet, connection);
                        break;

                    case "UPDATE_PROFILE":
                        await AuthHandler.HandleUpdateProfileAsync(packet, connection);
                        break;

                    case "CREATE_ROOM":
                        await RoomHandler.HandleCreateRoomAsync(packet, connection);
                        break;

                    case "GET_ROOM_LIST":
                        await RoomHandler.HandleGetRoomListAsync(connection);
                        break;

                    case "JOIN_ROOM":
                        await RoomHandler.HandleJoinRoomAsync(packet, connection);
                        break;

                    case "PLAYER_READY":
                        await RoomHandler.HandlePlayerReadyAsync(packet, connection);
                        break;

                    case "START_GAME":
                        await RoomHandler.HandleStartGameAsync(packet, connection);
                        break;

                    case "LEAVE_ROOM":
                        await GameHandler.HandleLeaveRoomAsync(connection, sendLeaveSuccess: true);
                        break;

                    case "LOGOUT":
                        await TcpServer.HandleDisconnectAsync(connection);
                        connection.TcpClient?.Close();
                        break;
                    case "ROLL_DICE":
                        await GameHandler.HandleDiceRollAsync(connection);
                        break;

                    case "END_TURN":
                        await GameHandler.HandleEndTurnAsync(connection);
                        break;

                    case "BUY_PROPERTY":
                        await GameHandler.HandleBuyPropertyAsync(connection);
                        break;

                    case "BUYOUT_PROPERTY":
                        await GameHandler.HandleBuyoutPropertyAsync(packet, connection);
                        break;

                    case "BUILD_PROPERTY":
                        await GameHandler.HandleBuildPropertyAsync(packet, connection);
                        break;

                    case "SELL_PROPERTY_FOR_DEBT":
                        await GameHandler.HandleSellPropertyForDebtAsync(packet, connection);
                        break;

                    case "USE_CARD":
                        await GameHandler.HandleUseCardAsync(packet, connection);
                        break;

                    case "CARD_CHOICE_MADE":
                        await GameHandler.HandleCardChoiceMadeAsync(packet, connection);
                        break;

                    case "RESUME_GAME":
                        await GameHandler.HandleResumeGameAsync(packet, connection);
                        break;

                    case "REQUEST_PAUSE":
                        await GameControlHandler.HandleRequestPauseAsync(connection);
                        break;

                    case "PAUSE_VOTE":
                        await GameControlHandler.HandlePauseVoteAsync(packet, connection);
                        break;

                    case "RESUME_GAMEPLAY":
                        await GameControlHandler.HandleResumeGameplayAsync(connection);
                        break;

                    case "SURRENDER_GAME":
                        await GameControlHandler.HandleSurrenderAsync(connection);
                        break;

                    case "CHAT_MESSAGE":
                        await RoomHandler.HandleGameChatAsync(packet, connection);
                        break;

                    case "GET_LEADERBOARD":
                        await AuthHandler.HandleGetLeaderboardAsync(connection);
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

        private static bool IsBlockedWhilePaused(string packetType, ClientConnection connection)
        {
            if (connection == null || string.IsNullOrWhiteSpace(connection.CurrentRoomId))
                return false;

            string[] blockedPacketTypes =
            {
                "ROLL_DICE",
                "END_TURN",
                "BUY_PROPERTY",
                "BUYOUT_PROPERTY",
                "BUILD_PROPERTY",
                "SELL_PROPERTY_FOR_DEBT",
                "USE_CARD",
                "CARD_CHOICE_MADE"
            };

            if (!blockedPacketTypes.Contains(packetType))
                return false;

            lock (ServerState.Lock)
            {
                return ServerState.Rooms.TryGetValue(connection.CurrentRoomId, out Room room) &&
                    room.GameState != null &&
                    room.GameState.IsPaused;
            }
        }
    }
}


