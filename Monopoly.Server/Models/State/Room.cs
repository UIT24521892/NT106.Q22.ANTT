using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Server.Models.State
{
    public class Room
    {
        public string RoomId { get; set; }
        public string HostUsername { get; set; }
        public int MaxPlayers { get; set; }
        public int BotCount { get; set; }
        public string MapName { get; set; }
        public bool IsStarted { get; set; }

        public List<RoomPlayer> Players { get; set; } = new List<RoomPlayer>();
        public GameState GameState { get; set; } = new GameState();
    }
}
