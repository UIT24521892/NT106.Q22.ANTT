using System.Collections.Generic;
using System.Net.Sockets;
using Monopoly.Server.Models;
using Monopoly.Server.GameLogic;

namespace Monopoly.Server.Models.State
{
    public static class ServerState
    {
        public static readonly Dictionary<NetworkStream, ClientConnection> Clients = new Dictionary<NetworkStream, ClientConnection>();
        public static readonly Dictionary<string, Room> Rooms = new Dictionary<string, Room>();
        public static readonly object Lock = new object();
        public static readonly System.Random Random = new System.Random();
        public static readonly DeckManager DeckManager = new DeckManager();
    }
}

