using System.Collections.Generic;

public static class GameSession
{
    public static string RoomId;
    public static string MapName;
    public static List<PlayerSlotData> Players;
    public static GameStateData CurrentState;

    public static void Initialize(
        string roomId,
        string mapName,
        List<PlayerSlotData> players,
        GameStateData gameState = null)
    {
        RoomId = roomId;
        MapName = mapName;
        Players = players;
        CurrentState = gameState;
    }

    public static void UpdateState(GameStateData gameState)
    {
        if (gameState == null)
            return;

         CurrentState = gameState;
        RoomId = gameState.RoomId;
        MapName = gameState.MapName;
    }

    public static void Clear()
    {
        RoomId = "";
        MapName = "";
        Players = null;
        CurrentState = null;
    }
}
