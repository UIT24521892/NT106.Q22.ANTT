using System;
using System.Collections.Generic;
using Monopoly.Shared.Models.Constants;

namespace Monopoly.Server.GameLogic
{
    public class DeckManager
    {
        private List<string> _deck;
        private Random _random;

        public DeckManager()
        {
            _deck = new List<string>();
            _random = new Random();
            InitializeDeck();
        }

        // Bước 1: Khởi tạo và phân bổ số lượng bài
        private void InitializeDeck()
        {
            _deck.Clear();

            // Duyệt qua toàn bộ thẻ gốc từ Database mà bạn vừa tạo
            foreach (var kvp in CardDatabase.Cards)
            {
                string cardId = kvp.Key;
                string cardType = kvp.Value.Type;

                int copies = 0;
                // Thiết lập tỷ lệ Vàng: 3 lá Vàng (hiếm), 10 lá Bạc (phổ biến), 7 lá Gỗ (phạt)
                if (cardType == "Golden") copies = 3;
                else if (cardType == "Silver") copies = 10;
                else if (cardType == "Wooden") copies = 7;

                // Bỏ các bản sao của lá bài này vào tụ
                for (int i = 0; i < copies; i++)
                {
                    _deck.Add(cardId);
                }
            }

            // Gọi hàm xào bài
            ShuffleDeck();
            Console.WriteLine($"[DeckManager] Đã khởi tạo và xào xong tụ bài: {_deck.Count} lá.");
        }

        // Bước 2: Thuật toán xáo trộn Fisher-Yates chuẩn xác
        private void ShuffleDeck()
        {
            int n = _deck.Count;
            while (n > 1)
            {
                n--;
                int k = _random.Next(n + 1);
                // Tráo đổi vị trí 2 lá bài
                string value = _deck[k];
                _deck[k] = _deck[n];
                _deck[n] = value;
            }
        }

        // Bước 3: Rút lá bài trên cùng (dùng khi người chơi giẫm vào ô Cơ Hội)
        public ChanceCardConfig DrawCard()
        {
            // Nếu tụ bài đã bị rút cạn sạch, tự động gom bài cũ và xào lại từ đầu
            if (_deck.Count == 0)
            {
                Console.WriteLine("[DeckManager] Tụ bài đã hết, đang tiến hành xào lại...");
                InitializeDeck();
            }

            // Lấy lá bài trên cùng (phần tử đầu tiên)
            string drawnCardId = _deck[0];

            // Rút xong thì xóa lá đó khỏi tụ
            _deck.RemoveAt(0);

            // Trả về thông tin chi tiết của lá bài để Server gửi xuống Client
            return CardDatabase.Cards[drawnCardId];
        }

        // Hàm phụ để kiểm tra xem còn bao nhiêu lá (tùy chọn)
        public int GetRemainingCardsCount()
        {
            return _deck.Count;
        }
    }
}