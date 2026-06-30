using Monopoly.Shared.Models.Configs.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Models.Configs.StaticData
{
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

            { "CARD_WOOD_01", new ChanceCardConfig
                { ID = "CARD_WOOD_01", Name = "Cúp Điện Toàn Thành Phố", Type = "Wooden", EffectCode = "POWER_OUTAGE",
                  DetailEffect = "Chọn 1 thành phố của đối thủ. Nó sẽ mất hiệu lực thu tiền phạt trong 2 lượt tới." } },
            { "CARD_WOOD_02", new ChanceCardConfig
                { ID = "CARD_WOOD_02", Name = "Động Đất", Type = "Wooden", EffectCode = "EARTHQUAKE",
                  DetailEffect = "Thiên tai! Phá huỷ làm tụt 1 cấp độ xây dựng của một thành phố ngẫu nhiên thuộc về đối thủ." } },
            { "CARD_WOOD_03", new ChanceCardConfig
                { ID = "CARD_WOOD_03", Name = "Biên Bản Phạt", Type = "Wooden", EffectCode = "FINE",
                  DetailEffect = "Vi phạm luật lệ giao thông. Bị trừ ngay 100,000 tiền mặt!" } },
            { "CARD_WOOD_04", new ChanceCardConfig
                { ID = "CARD_WOOD_04", Name = "Tình Nghi Gian Lận", Type = "Wooden", EffectCode = "GO_TO_JAIL",
                  DetailEffect = "Bắt quả tang gian lận! Lập tức bị đày ra Đảo Hoang nghỉ mát." } },
            { "CARD_WOOD_05", new ChanceCardConfig
                { ID = "CARD_WOOD_05", Name = "Đóng Băng Giao Dịch", Type = "Wooden", EffectCode = "SKIP_TURN",
                  DetailEffect = "Tài khoản bị phong tỏa tạm thời. Bạn bị mất lượt đi tiếp theo!" } },
            { "CARD_WOOD_06", new ChanceCardConfig
                { ID = "CARD_WOOD_06", Name = "Thanh Tra Thuế", Type = "Wooden", EffectCode = "TAX_PENALTY",
                  DetailEffect = "Thanh tra đột xuất! Bắt buộc nộp phạt 10% tổng số tiền mặt hiện có cho Ngân hàng." } }
        };
    }
}
