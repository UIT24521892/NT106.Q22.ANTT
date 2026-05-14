using System.Collections.Generic;

public static class GameSession
{
    public static string RoomId;
    public static string MapName;
    public static List<PlayerSlotData> Players;

    public static void Initialize(string roomId, string mapName, List<PlayerSlotData> players)
    {
        RoomId = roomId;
        MapName = mapName;
        Players = players;
    }

    public static void Clear()
    {
        RoomId = "";
        MapName = "";
        Players = null;
    }
}