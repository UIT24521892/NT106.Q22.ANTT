using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Server.Models.State
{
    public enum BotPersonality
    {
        Balanced,
        Aggressive,
        Conservative
    }

    public class GamePlayerState
    {
        public string Username { get; set; } = "";
        public string AvatarId { get; set; } = "avatar_1";
        public bool IsBot { get; set; }
        public BotPersonality Personality { get; set; } = BotPersonality.Balanced;
        public int PlayerIndex { get; set; }
        public int Position { get; set; }
        public long Money { get; set; }
        public bool IsBankrupt { get; set; }
        public int BankruptcyOrder { get; set; }
        public bool IsConnected { get; set; } = true;
        public int ConsecutiveDoubles { get; set; }
        public int JailTurnsLeft { get; set; }
        public bool HasFreeRentCard { get; set; }
        public bool IsFreeRentShieldActive { get; set; }
        public bool HasEscapeIslandCard { get; set; }
        public bool HasFlightCard { get; set; }
        public bool HasFreeUpgradeCard { get; set; }
        public bool HasForceDoubleCard { get; set; }
        public bool HasEarthquakeCard { get; set; }
        public bool HasPowerOutageCard { get; set; }
        public bool HasMoveChampionshipCard { get; set; }
        public bool IsOnIsland { get; set; }
        public int SkipTurnsLeft { get; set; }
        public string SkipReason { get; set; } = "";
        public string LastDrawnCardId { get; set; } = "";
    }
}
