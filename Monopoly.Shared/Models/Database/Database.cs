using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Models.Database
{
    internal class Database
    {
    }
    // Bảng USERS
    public class UserModel
    {
        public string UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public int Point { get; set; }
        public int TotalWins { get; set; }
        public int TotalLosses { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    // 1. THẺ BÀI (Đã gọt bỏ các Khóa ngoại thừa thãi)
    public class InventoryCardModel
    {
        // Bỏ MatchPlayerID vì nó đã nằm gọn trong bụng Player rồi
        public string CardCode { get; set; }
        public int AcquireAt { get; set; }
    }

    // 2. Ô ĐẤT
    public class MatchPropertyModel
    {
        // Bỏ PositionIndex và RoomID khỏi ruột JSON, 
        // vì PositionIndex sẽ được dùng làm Key của Dictionary bên dưới
        public string Owner_MatchPlayerID { get; set; }
        public int HouseCount { get; set; }
        public bool HasHotel { get; set; }
        public int Multiplier { get; set; }
        public int PowerOutageTurn { get; set; }
    }

    // 3. NGƯỜI CHƠI (Chứa Dictionary Thẻ Bài)
    public class MatchPlayerModel
    {
        public string UserID { get; set; } // Vẫn giữ để biết ai là ai
        public bool IsBot { get; set; }

        public int Position { get; set; }
        public int ConsecutiveDoubles { get; set; }
        public int JailTurnsLeft { get; set; }
        public bool IsBankrupt { get; set; }

        public long TotalAssetValue { get; set; }
        public long Money { get; set; }
        // ... (các biến tài sản, bộ đếm khác giữ nguyên)
        // Sử dụng Dictionary để lưu trữ dạng JSON Object: {"Red": 1, "Blue": 2}
        public Dictionary<string, int> OwnedColors { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> OwnedLines { get; set; } = new Dictionary<string, int>();

        public int ResortCount { get; set; }
        public int CompletedColorSets { get; set; }
        public int CompletedLines { get; set; }
        // TÚI ĐỒ NESTING TẠI ĐÂY
        // Key là InventoryID (do Firebase sinh ra), Value là thông tin Thẻ
        public Dictionary<string, InventoryCardModel> InventoryCards { get; set; } = new Dictionary<string, InventoryCardModel>();
    }

    // 4. PHÒNG CHƠI (Trùm cuối - Chứa tất cả)
    public class RoomModel
    {
        public string RoomID { get; set; }  //Room01
        public string RoomType { get; set; }    //ranking, normal
        public string Host_UserID { get; set; } //User01, User02, etc
        public string Status { get; set; }  // "Waiting", "Playing", "Finished"
        public int MaxPlayers { get; set; } //2, 3, 4
        public DateTime GameEndTime { get; set; }
        public string CurrentTurn_MatchPlayerID { get; set; }
        // ... (các cấu hình phòng khác giữ nguyên)
        public DateTime TurnEndTime { get; set; }
        public int WorldChampionshipPos { get; set; } = -1; // Mặc định -1 theo chuẩn
        // PLAYERS NESTING TẠI ĐÂY (Key = MatchPlayerID)
        public Dictionary<string, MatchPlayerModel> Players { get; set; } = new Dictionary<string, MatchPlayerModel>();

        // PROPERTIES NESTING TẠI ĐÂY (Key = PositionIndex dạng string "0", "1", "39")
        public Dictionary<string, MatchPropertyModel> Properties { get; set; } = new Dictionary<string, MatchPropertyModel>();
    }
}
