using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Server.Models.State
{
    public class RoomPlayer
    {
        public string Username { get; set; }
        public bool IsReady { get; set; }
        public bool IsHost { get; set; }
        public bool IsBot { get; set; }
        public int PlayerIndex { get; set; }
        public string AvatarId { get; set; } = "avatar_1";
    }
}
