using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Models.Configs.Models
{
    public class ChanceCardConfig
    {
        public string ID { get; set; } // Ví dụ: "CARD_01"
        public string Name { get; set; }
        public string DetailEffect { get; set; }
        public string EffectCode { get; set; } // Ví dụ: "GO_TO_JAIL"
        public string Type { get; set; } // "Golden", "Silver", "Wooden"
    }
}
