using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Monopoly.Server.Models.Events
{
    public class GameOverRankingResult
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int Rank { get; set; }
        public int ScoreEarned { get; set; }

        [JsonIgnore]
        public string IdToken { get; set; } = "";
    }
}
