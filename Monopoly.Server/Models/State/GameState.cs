using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Server.Models.State
{
    public class GameState
    {
        public string RoomId { get; set; } = "";
        public string MapName { get; set; } = "";
        public int TurnNumber { get; set; } = 1;
        public int CurrentTurnPlayerIndex { get; set; }
        public string CurrentTurnUsername { get; set; } = "";
        public int LastDice1 { get; set; }
        public int LastDice2 { get; set; }
        public int LastDiceTotal { get; set; }
        public int LastMovedPlayerIndex { get; set; } = -1;
        public int LastMoveFromPosition { get; set; } = -1;
        public int LastMoveToPosition { get; set; } = -1;
        public int LastFinalPosition { get; set; } = -1;
        public string LastActionMessage { get; set; } = "";
        public bool HasRolledThisTurn { get; set; }
        public int TurnDurationSeconds { get; set; } = 45;
        public long TurnEndsAtUtcTicks { get; set; }
        public long ServerUtcTicks { get; set; }
        public bool IsFinished { get; set; }
        public string WinnerUsername { get; set; } = "";
        public string MatchId { get; set; } = "";
        public bool GameOverBroadcasted { get; set; }
        public int MatchDurationSeconds { get; set; } = 1200;
        public long MatchStartedAtUtcTicks { get; set; }
        public long MatchEndsAtUtcTicks { get; set; }
        public string EndReason { get; set; } = "";
        public bool IsPaused { get; set; }
        public string PauseRequestedBy { get; set; } = "";
        public long PauseStartedAtUtcTicks { get; set; }
        public List<string> PauseVotes { get; set; } = new List<string>();
        public int WorldChampionshipPosition { get; set; } = 16;
        public bool IsWaitingForCardChoice { get; set; }
        public string PendingCardEffectCode { get; set; } = "";
        public string PendingCardPlayerUsername { get; set; } = "";
        public List<int> PendingCardTargetPositions { get; set; } = new List<int>();
        public bool ForceDoubleThisTurn { get; set; }
        public bool IsWaitingForPropertySale { get; set; }
        public int PendingSalePlayerIndex { get; set; } = -1;
        public string PendingSalePlayerUsername { get; set; } = "";
        public long PendingDebtAmount { get; set; }
        public int PendingDebtCreditorPlayerIndex { get; set; } = -1;
        public string PendingDebtReason { get; set; } = "";
        public List<int> PendingSalePropertyPositions { get; set; } = new List<int>();
        public bool IsBotPlaying { get; set; }
        public List<string> ActionLog { get; set; } = new List<string>();

        public List<GamePlayerState> Players { get; set; } = new List<GamePlayerState>();
        public Dictionary<int, GamePropertyState> Properties { get; set; } =
            new Dictionary<int, GamePropertyState>();
    }
}
