using System.Collections.Generic;

[System.Serializable]
public class RoomSummaryData
{
    public string RoomId;
    public string HostUsername;
    public int CurrentPlayers;
    public int MaxPlayers;
    public int BotCount;
    public string MapName;
}

[System.Serializable]
public class GameStateData
{
    public string RoomId;
    public string MapName;
    public int TurnNumber;
    public int CurrentTurnPlayerIndex;
    public string CurrentTurnUsername;
    public int LastDice1;
    public int LastDice2;
    public int LastDiceTotal;
    public int LastMovedPlayerIndex;
    public int LastMoveFromPosition;
    public int LastMoveToPosition;
    public int LastFinalPosition;
    public string LastActionMessage;
    public bool HasRolledThisTurn;
    public int TurnDurationSeconds;
    public long TurnEndsAtUtcTicks;
    public long ServerUtcTicks;
    public bool IsFinished;
    public string WinnerUsername;
    public int WorldChampionshipPosition;
    public bool IsWaitingForCardChoice;
    public string PendingCardEffectCode;
    public string PendingCardPlayerUsername;
    public List<int> PendingCardTargetPositions;
    public bool ForceDoubleThisTurn;
    public bool IsWaitingForPropertySale;
    public int PendingSalePlayerIndex;
    public string PendingSalePlayerUsername;
    public long PendingDebtAmount;
    public int PendingDebtCreditorPlayerIndex;
    public string PendingDebtReason;
    public List<int> PendingSalePropertyPositions;
    public List<string> ActionLog;

    public List<GamePlayerStateData> Players;
    public Dictionary<int, GamePropertyStateData> Properties;
}

[System.Serializable]
public class ChatMessageData
{
    public string RoomId;
    public string Username;
    public string Message;
    public long SentAtUtcTicks;
}

[System.Serializable]
public class GameOverData
{
    public string MatchId;
    public List<RankingEntryData> Rankings;
}

[System.Serializable]
public class RankingEntryData
{
    public string UserId;
    public string DisplayName;
    public int Rank;
    public int ScoreEarned;
}

[System.Serializable]
public class LeaderboardData
{
    public List<LeaderboardEntryData> Entries;
}

[System.Serializable]
public class LeaderboardEntryData
{
    public string UserId;
    public string DisplayName;
    public int Rank;
    public int Score;
    public int Wins;
    public int TotalMatches;
}

[System.Serializable]
public class GamePlayerStateData
{
    public string Username;
    public bool IsBot;
    public int PlayerIndex;
    public int Position;
    public long Money;
    public bool IsBankrupt;
    public int BankruptcyOrder;
    public bool IsConnected;
    public int ConsecutiveDoubles;
    public int JailTurnsLeft;
    public bool HasFreeRentCard;
    public bool IsFreeRentShieldActive;
    public bool HasEscapeIslandCard;
    public bool HasFlightCard;
    public bool HasFreeUpgradeCard;
    public bool HasForceDoubleCard;
    public bool HasEarthquakeCard;
    public bool HasPowerOutageCard;
    public bool HasMoveChampionshipCard;
    public bool IsOnIsland;
    public int SkipTurnsLeft;
    public string SkipReason;
    public string LastDrawnCardId;
}

[System.Serializable]
public class GamePropertyStateData
{
    public int PositionIndex;
    public string Name;
    public string Type;
    public string ColorSet;
    public string LineIndex;
    public long BuyPrice;
    public List<long> RentPrices;
    public int OwnerPlayerIndex;
    public int HouseCount;
    public bool HasHotel;
    public int Multiplier;
    public int PowerOutageTurn;
}
