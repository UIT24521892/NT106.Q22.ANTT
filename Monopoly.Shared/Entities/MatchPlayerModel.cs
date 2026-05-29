using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Entities
{
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
}
