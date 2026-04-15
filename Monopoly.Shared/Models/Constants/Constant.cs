using System.Collections.Generic;

namespace Monopoly.Shared.Models.Constants
{
    // --- 1. CẤU HÌNH VÀ DỮ LIỆU BÀN CỜ ---
    public class BoardSquareConfig
    {
        public int PositionIndex { get; set; }
        public string Name { get; set; }
        public string Type { get; set; } // "Start", "City", "Chance", "Resort", v.v.
        public long BuyPrice { get; set; }

        // Mảng giá phạt tương ứng với: [Gốc, 1 Nhà, 2 Nhà, 3 Nhà, Khách Sạn]
        public List<long> RentPrices { get; set; } = new List<long>();

        public string ColorSet { get; set; }
        public string LineIndex { get; set; }
    }

    public static class BoardDatabase
    {
        // Danh sách 32 ô đất theo chuẩn Business Tour
        public static readonly Dictionary<int, BoardSquareConfig> Squares = new Dictionary<int, BoardSquareConfig>
        {
            { 0, new BoardSquareConfig {
                PositionIndex = 0, Name = "Bắt Đầu", Type = "Start", LineIndex = "1"
            }},
            { 1, new BoardSquareConfig {
                PositionIndex = 1, Name = "Tokyo", Type = "City", ColorSet = "Pink", LineIndex = "1",
                BuyPrice = 50000, RentPrices = new List<long> { 2000, 10000, 30000, 90000, 250000 }
            }},
            { 2, new BoardSquareConfig {
                PositionIndex = 2, Name = "Cơ quan Thuế", Type = "Tax", LineIndex = "1"
            }},
            { 3, new BoardSquareConfig {
                PositionIndex = 3, Name = "Osaka", Type = "City", ColorSet = "Pink", LineIndex = "1",
                BuyPrice = 60000, RentPrices = new List<long> { 4000, 20000, 60000, 180000, 450000 }
            }},
            { 4, new BoardSquareConfig {
                PositionIndex = 4, Name = "Cơ Hội", Type = "Chance", LineIndex = "1"
            }},
            { 5, new BoardSquareConfig {
                PositionIndex = 5, Name = "Paris", Type = "City", ColorSet = "Yellow", LineIndex = "1",
                BuyPrice = 100000, RentPrices = new List<long> { 6000, 30000, 90000, 270000, 550000 }
            }},
            { 6, new BoardSquareConfig {
                PositionIndex = 6, Name = "Lyon", Type = "City", ColorSet = "Yellow", LineIndex = "1",
                BuyPrice = 120000, RentPrices = new List<long> { 8000, 40000, 100000, 300000, 600000 }
            }},
            { 7, new BoardSquareConfig {
                PositionIndex = 7, Name = "Nice", Type = "Resort", LineIndex = "1",
                BuyPrice = 200000, RentPrices = new List<long> { 50000 } // Resort fixed theo logic entity.md
            }},
            
            
            
            { 39, new BoardSquareConfig {
                PositionIndex = 39, Name = "Hà Nội", Type = "City",
                BuyPrice = 400000,
                RentPrices = new List<long> { 50000, 200000, 600000, 1400000, 2000000 },
                ColorSet = "Blue",
                LineIndex = "4"      // Nằm ở cạnh cuối cùng
            }}
        };
    }

    // --- 2. CẤU HÌNH VÀ DỮ LIỆU THẺ BÀI ---
    public class ChanceCardConfig
    {
        public string ID { get; set; } // Ví dụ: "CARD_01"
        public string Name { get; set; }
        public string DetailEffect { get; set; }
        public string EffectCode { get; set; } // Ví dụ: "GO_TO_JAIL"
        public string Type { get; set; } // "Golden", "Silver", "Wooden"
    }

    // Đã thêm từ khóa "static"
    public static class CardDatabase
    {
        // Đã đổi Key của Dictionary thành "string" để khớp với CardCode từ Firebase
        public static readonly Dictionary<string, ChanceCardConfig> Cards = new Dictionary<string, ChanceCardConfig>()
        {
            { "CARD_WOOD_01", new ChanceCardConfig // Key tra cứu chuẩn
                {
                    ID = "CARD_WOOD_01",
                    Name = "Vào Tù",
                    DetailEffect = "Bạn bị tình nghi gian lận thương mại. Đi thẳng vào tù!",
                    EffectCode = "GO_TO_JAIL",
                    Type = "Wooden"
                }
            },
            { "CARD_GOLD_01", new ChanceCardConfig
                {
                    ID = "CARD_GOLD_01",
                    Name = "Thẻ Miễn Phí",
                    DetailEffect = "Được miễn phí một lần trả tiền thuê đất.",
                    EffectCode = "FREE_RENT",
                    Type = "Golden"
                }
            }
            //...
        };
    }
}