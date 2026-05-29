using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Entities
{
    public class MatchPropertyModel
    {
        // Bỏ PositionIndex và RoomID khỏi ruột JSON, 
        // vì PositionIndex sẽ được dùng làm Key của Dictionary bên dưới
        public string Owner_MatchPlayerID { get; set; }
        public int HouseCount { get; set; }
        public bool HasHotel { get; set; }
        public int Multiplier { get; set; }
        public int PowerOutageTurn { get; set; }
    }
}
