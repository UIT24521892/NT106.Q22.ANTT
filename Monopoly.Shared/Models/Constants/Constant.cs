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
            { 8, new BoardSquareConfig {
                PositionIndex = 8, Name = "Du Lịch Thế Giới", Type = "WorldTour", LineIndex = "2"
            }},
            { 9, new BoardSquareConfig {
                PositionIndex = 9, Name = "New York", Type = "City", ColorSet = "Blue", LineIndex = "2",
                BuyPrice = 140000, RentPrices = new List<long> { 10000, 50000, 150000, 450000, 750000 }
            }},
            { 10, new BoardSquareConfig {
                PositionIndex = 10, Name = "Las Vegas", Type = "City", ColorSet = "Blue", LineIndex = "2",
                BuyPrice = 140000, RentPrices = new List<long> { 10000, 50000, 150000, 450000, 750000 }
            }},
            { 11, new BoardSquareConfig {
                PositionIndex = 11, Name = "Chicago", Type = "City", ColorSet = "Blue", LineIndex = "2",
                BuyPrice = 160000, RentPrices = new List<long> { 12000, 60000, 180000, 500000, 900000 }
            }},
            { 12, new BoardSquareConfig {
                PositionIndex = 12, Name = "Cơ Hội", Type = "Chance", LineIndex = "2"
            }},
            { 13, new BoardSquareConfig {
                PositionIndex = 13, Name = "Sydney", Type = "City", ColorSet = "Green", LineIndex = "2",
                BuyPrice = 180000, RentPrices = new List<long> { 14000, 70000, 200000, 550000, 950000 }
            }},
            { 14, new BoardSquareConfig {
                PositionIndex = 14, Name = "Dubai", Type = "Resort", LineIndex = "2",
                BuyPrice = 200000, RentPrices = new List<long> { 50000 }
            }},
            { 15, new BoardSquareConfig {
                PositionIndex = 15, Name = "London", Type = "City", ColorSet = "Green", LineIndex = "2",
                BuyPrice = 200000, RentPrices = new List<long> { 16000, 80000, 220000, 600000, 1000000 }
            }},

            { 16, new BoardSquareConfig {
                PositionIndex = 16, Name = "Giải Vô Địch", Type = "WorldChampionship", LineIndex = "3"
            }},
            { 17, new BoardSquareConfig {
                PositionIndex = 17, Name = "Berlin", Type = "City", ColorSet = "Brown", LineIndex = "3",
                BuyPrice = 220000, RentPrices = new List<long> { 18000, 90000, 250000, 700000, 1050000 }
            }},
            { 18, new BoardSquareConfig {
                PositionIndex = 18, Name = "Cyprus", Type = "Resort", LineIndex = "3",
                BuyPrice = 200000, RentPrices = new List<long> { 50000 }
            }},
            { 19, new BoardSquareConfig {
                PositionIndex = 19, Name = "Hamburg", Type = "City", ColorSet = "Brown", LineIndex = "3",
                BuyPrice = 240000, RentPrices = new List<long> { 20000, 100000, 300000, 750000, 1100000 }
            }},
            { 20, new BoardSquareConfig {
                PositionIndex = 20, Name = "Cơ Hội", Type = "Chance", LineIndex = "3"
            }},
            { 21, new BoardSquareConfig {
                PositionIndex = 21, Name = "Rome", Type = "City", ColorSet = "Purple", LineIndex = "3",
                BuyPrice = 260000, RentPrices = new List<long> { 22000, 110000, 330000, 800000, 1150000 }
            }},
            { 22, new BoardSquareConfig {
                PositionIndex = 22, Name = "Milan", Type = "City", ColorSet = "Purple", LineIndex = "3",
                BuyPrice = 260000, RentPrices = new List<long> { 22000, 110000, 330000, 800000, 1150000 }
            }},
            { 23, new BoardSquareConfig {
                PositionIndex = 23, Name = "Venice", Type = "City", ColorSet = "Purple", LineIndex = "3",
                BuyPrice = 280000, RentPrices = new List<long> { 24000, 120000, 360000, 850000, 1200000 }
            }},

             { 24, new BoardSquareConfig {
                PositionIndex = 24, Name = "Đảo Hoang", Type = "LostIsland", LineIndex = "4"
            }},
            { 25, new BoardSquareConfig {
                PositionIndex = 25, Name = "Shanghai", Type = "City", ColorSet = "Orange", LineIndex = "4",
                BuyPrice = 300000, RentPrices = new List<long> { 26000, 130000, 390000, 900000, 1275000 }
            }},
            { 26, new BoardSquareConfig {
                PositionIndex = 26, Name = "Beijing", Type = "City", ColorSet = "Orange", LineIndex = "4",
                BuyPrice = 300000, RentPrices = new List<long> { 26000, 130000, 390000, 900000, 1275000 }
            }},
            { 27, new BoardSquareConfig {
                PositionIndex = 27, Name = "Hong Kong", Type = "City", ColorSet = "Orange", LineIndex = "4",
                BuyPrice = 320000, RentPrices = new List<long> { 28000, 150000, 450000, 1000000, 1400000 }
            }},
            { 28, new BoardSquareConfig {
                PositionIndex = 28, Name = "Bali", Type = "Resort", LineIndex = "4",
                BuyPrice = 200000, RentPrices = new List<long> { 50000 }
            }},
            { 29, new BoardSquareConfig {
                PositionIndex = 29, Name = "Madrid", Type = "City", ColorSet = "Cyan", LineIndex = "4",
                BuyPrice = 350000, RentPrices = new List<long> { 35000, 175000, 500000, 1100000, 1500000 }
            }},
            { 30, new BoardSquareConfig {
                PositionIndex = 30, Name = "Seville", Type = "City", ColorSet = "Cyan", LineIndex = "4",
                BuyPrice = 350000, RentPrices = new List<long> { 35000, 175000, 500000, 1100000, 1500000 }
            }},
            { 31, new BoardSquareConfig {
                PositionIndex = 31, Name = "Granada", Type = "City", ColorSet = "Cyan", LineIndex = "4",
                BuyPrice = 400000, RentPrices = new List<long> { 50000, 200000, 600000, 1400000, 2000000 }
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
            { "CARD_GOLD_01", new ChanceCardConfig
                { ID = "CARD_GOLD_01", Name = "Khiên Miễn Trừ", Type = "Golden", EffectCode = "FREE_RENT",
                  DetailEffect = "Thẻ Miễn Phí: Không phải trả tiền phạt khi giẫm vào khu đất đắt đỏ của đối thủ." } },
            { "CARD_GOLD_02", new ChanceCardConfig
                { ID = "CARD_GOLD_02", Name = "Vé Máy Bay Hạng Thương Gia", Type = "Golden", EffectCode = "FLIGHT",
                  DetailEffect = "Thẻ Bay: Chọn bay thẳng đến bất kỳ ô nào trên bàn cờ. Nếu đi ngang Bắt Đầu, nhận 300,000." } },
            { "CARD_GOLD_03", new ChanceCardConfig
                { ID = "CARD_GOLD_03", Name = "Trực Thăng Cứu Hộ", Type = "Golden", EffectCode = "ESCAPE_ISLAND",
                  DetailEffect = "Thẻ Ra Đảo: Dùng để thoát khỏi Đảo Hoang ngay lập tức mà không tốn 200,000." } },
            { "CARD_GOLD_04", new ChanceCardConfig
                { ID = "CARD_GOLD_04", Name = "Giấy Phép Xây Dựng", Type = "Golden", EffectCode = "FREE_UPGRADE",
                  DetailEffect = "Thẻ Nâng Cấp: Tự động nâng cấp miễn phí 1 bậc cho một thành phố của bạn." } },
            
            // --- NEW: Tính năng ép xúc xắc của Business Tour ---
            { "CARD_GOLD_05", new ChanceCardConfig
                { ID = "CARD_GOLD_05", Name = "Xúc Xắc Ma Thuật", Type = "Golden", EffectCode = "FORCE_DOUBLE",
                  DetailEffect = "Sử dụng trước khi đổ: Lượt xúc xắc này của bạn chắc chắn sẽ ra số Đôi." } },
            { "CARD_SILVER_06", new ChanceCardConfig
                { ID = "CARD_SILVER_06", Name = "Chuyến Bay Đêm", Type = "Silver", EffectCode = "GO_TO_WORLD_TOUR",
                  DetailEffect = "Bay khẩn cấp! Đi thẳng đến ô Du Lịch Thế Giới (Sân bay) để chờ cất cánh vào lượt sau." } },
            { "CARD_SILVER_07", new ChanceCardConfig
                { ID = "CARD_SILVER_07", Name = "Đăng Cai Giải Đấu", Type = "Silver", EffectCode = "MOVE_CHAMPIONSHIP",
                  DetailEffect = "Quyền lực tối thượng: Lập tức dời Giải Vô Địch Thế Giới về một thành phố của bạn!" } },
            { "CARD_SILVER_08", new ChanceCardConfig
                { ID = "CARD_SILVER_08", Name = "Trúng Số Độc Đắc", Type = "Silver", EffectCode = "JACKPOT",
                  DetailEffect = "Trúng giải Vietlott! Nhận ngay 500,000 từ Ngân hàng." } },
            { "CARD_SILVER_09", new ChanceCardConfig
                { ID = "CARD_SILVER_09", Name = "Quỹ Từ Thiện", Type = "Silver", EffectCode = "CHARITY_PAY",
                  DetailEffect = "Bạn có lòng hảo tâm. Bắt buộc trích 50,000 tiền mặt tặng cho mỗi người chơi khác." } },


        };
    }
}