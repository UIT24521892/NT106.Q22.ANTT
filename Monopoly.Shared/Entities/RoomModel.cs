using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Entities
{
    public class RoomModel
    {
        public string RoomID { get; set; }  //Room01
        public string RoomType { get; set; }    //ranking, normal
        public string Host_UserID { get; set; } //User01, User02, etc
        public string Status { get; set; }  // "Waiting", "Playing", "Finished"
        public int MaxPlayers { get; set; } //2, 3, 4
        public DateTime GameEndTime { get; set; }
        public string CurrentTurn_MatchPlayerID { get; set; }
        // ... (các cấu hình phòng khác giữ nguyên)
        public DateTime TurnEndTime { get; set; }
        public int WorldChampionshipPos { get; set; } = -1; // Mặc định -1 theo chuẩn
        // PLAYERS NESTING TẠI ĐÂY (Key = MatchPlayerID)
        public Dictionary<string, MatchPlayerModel> Players { get; set; } = new Dictionary<string, MatchPlayerModel>();

        // PROPERTIES NESTING TẠI ĐÂY (Key = PositionIndex dạng string "0", "1", "39")
        public Dictionary<string, MatchPropertyModel> Properties { get; set; } = new Dictionary<string, MatchPropertyModel>();
    }
}
