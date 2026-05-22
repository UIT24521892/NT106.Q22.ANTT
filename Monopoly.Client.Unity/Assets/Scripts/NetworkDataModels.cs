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
    public string LastActionMessage;
    public bool HasRolledThisTurn;
    public int TurnDurationSeconds;
    public long TurnEndsAtUtcTicks;
    public long ServerUtcTicks;
    public bool IsFinished;
    public string WinnerUsername;
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
public class GamePlayerStateData
{
    public string Username;
    public bool IsBot;
    public int PlayerIndex;
    public int Position;
    public long Money;
    public bool IsBankrupt;
    public bool IsConnected;
    public int ConsecutiveDoubles;
    public int JailTurnsLeft;
}

[System.Serializable]
public class GamePropertyStateData
{
    public int PositionIndex;
    public string Name;
    public string Type;
    public long BuyPrice;
    public int OwnerPlayerIndex;
    public int HouseCount;
    public bool HasHotel;
    public int Multiplier;
    public int PowerOutageTurn;
}
