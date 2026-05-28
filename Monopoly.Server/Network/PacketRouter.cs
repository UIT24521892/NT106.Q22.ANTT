using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Monopoly.Server.Handles;
using Monopoly.Server.Models;
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

                switch (packetType)
                {
                    case "Login":
                    case "Register":
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

                    case "DiceRoll":
                    case "ROLL_DICE":
                        await GameHandler.HandleDiceRollAsync(connection);
                        break;

                    case "EndTurn":
                    case "END_TURN":
                        await GameHandler.HandleEndTurnAsync(connection);
                        break;

                    case "BUY_PROPERTY":
                    case "BuyProperty":
                        await GameHandler.HandleBuyPropertyAsync(connection);
                        break;

                    case "BUILD_PROPERTY":
                    case "BuildProperty":
                        await GameHandler.HandleBuildPropertyAsync(packet, connection);
                        break;

                    case "RESUME_GAME":
                        await GameHandler.HandleResumeGameAsync(packet, connection);
                        break;

                    case "GAME_CHAT":
                    case "CHAT_MESSAGE":
                        await RoomHandler.HandleGameChatAsync(packet, connection);
                        break;

                    case "GET_LEADERBOARD":
                        await AuthHandler.HandleGetLeaderboardAsync(connection);
                        break;

                    default:
                        Console.WriteLine($"[C?NH BÁO] Packet không xác d?nh: {packetType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[L?I X? LÝ GÓI TIN] {ex.Message}");
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
    }
}


