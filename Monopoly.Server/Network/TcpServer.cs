using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Monopoly.Server.Models.State;
using Monopoly.Server.Handles;

using Monopoly.Server.Models;

namespace Monopoly.Server.Network
{
    // ============================================================
    // HANDLE CLIENT
    // Dùng pending buffer d? tránh l?i TCP b? dính/v? packet.
    // ============================================================
    public static class TcpServer
    {
        public static async Task HandleClientAsync(TcpClient tcpClient)
        {
            ClientConnection connection = null;

            try
            {
                NetworkStream stream = tcpClient.GetStream();

                connection = new ClientConnection
                {
                    TcpClient = tcpClient,
                    Stream = stream
                };

                lock (ServerState.Lock)
                {
                    ServerState.Clients[stream] = connection;
                }

                byte[] buffer = new byte[4096];
                string pending = "";

                while (tcpClient.Connected)
                {
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    pending += Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    while (pending.Contains("<EOF>"))
                    {
                        int eofIndex = pending.IndexOf("<EOF>", StringComparison.Ordinal);
                        string jsonPacket = pending.Substring(0, eofIndex).Trim();

                        pending = pending.Substring(eofIndex + "<EOF>".Length);

                        if (string.IsNullOrWhiteSpace(jsonPacket))
                        {
                            continue;
                        }

                        Console.WriteLine($"[NH?N T? {tcpClient.Client.RemoteEndPoint}] {jsonPacket}");

                        await PacketRouter.RoutePacketAsync(jsonPacket, connection);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[L?I] K?t n?i b? ng?t: {ex.Message}");
            }
            finally
            {
                if (connection != null)
                {
                    await HandleDisconnectAsync(connection);
                }

                tcpClient.Close();

                Console.WriteLine("[DISCONNECT] M?t client dã r?i di.");
            }
        }
        public static async Task HandleDisconnectAsync(ClientConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            bool existed;

            lock (ServerState.Lock)
            {
                existed = ServerState.Clients.Remove(connection.Stream);
            }

            if (!existed)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(connection.CurrentRoomId))
            {
                await GameHandler.HandleLeaveRoomAsync(connection, sendLeaveSuccess: false);
            }
        }
    }
}



