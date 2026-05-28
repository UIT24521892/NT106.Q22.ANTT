using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Server.Models.Events
{
    public class LeaderboardEntry
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Rank { get; set; }
        public int Score { get; set; }
        public int Wins { get; set; }
        public int TotalMatches { get; set; }
    }
}
