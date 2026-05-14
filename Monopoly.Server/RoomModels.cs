using System.Collections.Generic;
using System.Net.Sockets;

namespace Monopoly.Server
{
    public class ClientConnection
    {
        public TcpClient TcpClient { get; set; }
        public NetworkStream Stream { get; set; }

        public string Uid { get; set; }
        public string Username { get; set; }
        public string CurrentRoomId { get; set; }
    }

    public class Room
    {
        public string RoomId { get; set; }
        public string HostUsername { get; set; }
        public int MaxPlayers { get; set; }
        public int BotCount { get; set; }
        public string MapName { get; set; }
        public bool IsStarted { get; set; }

        public List<RoomPlayer> Players { get; set; } = new List<RoomPlayer>();
    }

    public class RoomPlayer
    {
        public string Username { get; set; }
        public bool IsReady { get; set; }
        public bool IsHost { get; set; }
        public bool IsBot { get; set; }
        public int PlayerIndex { get; set; }
    }
}