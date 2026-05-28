using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Entities
{
    public class InventoryCardModel
    {
        // Bỏ MatchPlayerID vì nó đã nằm gọn trong bụng Player rồi
        public string CardCode { get; set; }
        public int AcquireAt { get; set; }
    }
}
