using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Server.Models.Events
{
    public class CardDrawEvent
    {
        public string DrawnByUsername { get; set; } = "";
        public string CardId { get; set; } = "";
        public string CardName { get; set; } = "";
        public string CardType { get; set; } = "";
        public string DetailEffect { get; set; } = "";
    }
}
