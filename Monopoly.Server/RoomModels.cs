using System.Collections.Generic;
using System.Net.Sockets;

namespace Monopoly.Server
{
    public class ClientConnection
    {
        public TcpClient TcpClient { get; set; }
        public NetworkStream Stream { get; set; }

        public string Uid { get; set; }
        public string Username { get; set; }
        public string CurrentRoomId { get; set; }
    }

    public class Room
    {
        public string RoomId { get; set; }
        public string HostUsername { get; set; }
        public int MaxPlayers { get; set; }
        public int BotCount { get; set; }
        public string MapName { get; set; }
        public bool IsStarted { get; set; }

        public List<RoomPlayer> Players { get; set; } = new List<RoomPlayer>();
        public GameState GameState { get; set; } = new GameState();
    }

    public class RoomPlayer
    {
        public string Username { get; set; }
        public bool IsReady { get; set; }
        public bool IsHost { get; set; }
        public bool IsBot { get; set; }
        public int PlayerIndex { get; set; }
    }

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
        public string LastActionMessage { get; set; } = "";
        public bool HasRolledThisTurn { get; set; }
        public int TurnDurationSeconds { get; set; } = 45;
        public long TurnEndsAtUtcTicks { get; set; }
        public long ServerUtcTicks { get; set; }
        public bool IsFinished { get; set; }
        public string WinnerUsername { get; set; } = "";
        public List<string> ActionLog { get; set; } = new List<string>();

        public List<GamePlayerState> Players { get; set; } = new List<GamePlayerState>();
        public Dictionary<int, GamePropertyState> Properties { get; set; } =
            new Dictionary<int, GamePropertyState>();
    }

    public class GamePlayerState
    {
        public string Username { get; set; } = "";
        public bool IsBot { get; set; }
        public int PlayerIndex { get; set; }
        public int Position { get; set; }
        public long Money { get; set; }
        public bool IsBankrupt { get; set; }
        public bool IsConnected { get; set; } = true;
        public int ConsecutiveDoubles { get; set; }
        public int JailTurnsLeft { get; set; }
    }

    public class GamePropertyState
    {
        public int PositionIndex { get; set; }
        public string Name { get; set; } = "";
        public string Type { get; set; } = "";
        public long BuyPrice { get; set; }
        public int OwnerPlayerIndex { get; set; } = -1;
        public int HouseCount { get; set; }
        public bool HasHotel { get; set; }
        public int Multiplier { get; set; } = 1;
        public int PowerOutageTurn { get; set; }
    }
}
