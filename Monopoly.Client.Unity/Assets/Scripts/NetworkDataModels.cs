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