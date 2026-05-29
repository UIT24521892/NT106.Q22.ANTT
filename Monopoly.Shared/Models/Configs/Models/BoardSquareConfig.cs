using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Models.Configs.Models
{
    public class BoardSquareConfig
    {
        public int PositionIndex { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // "Start", "City", "Chance", "Resort", v.v.
        public long BuyPrice { get; set; }

        // Mảng giá phạt tương ứng với: [Gốc, 1 Nhà, 2 Nhà, 3 Nhà, Khách Sạn]
        public List<long> RentPrices { get; set; } = new List<long>();

        public string ColorSet { get; set; }
        public string LineIndex { get; set; }
    }
}
