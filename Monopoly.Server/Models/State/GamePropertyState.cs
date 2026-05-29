using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Server.Models.State
{
    public class GamePropertyState
    {
        public int PositionIndex { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public string ColorSet { get; set; } = "";
        public string LineIndex { get; set; } = "";
        public long BuyPrice { get; set; }
        public List<long> RentPrices { get; set; } = new List<long>();
        public int OwnerPlayerIndex { get; set; } = -1;
        public int HouseCount { get; set; }
        public bool HasHotel { get; set; }
        public int Multiplier { get; set; } = 1;
        public int PowerOutageTurn { get; set; }
    }
}
