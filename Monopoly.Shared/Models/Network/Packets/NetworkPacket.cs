using Monopoly.Shared.Models.Network.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Models.Network.Packets
{
    public class NetworkPacket
    {
        public PacketType Type { get; set; }
        public string Payload { get; set; } // Chứa chuỗi JSON của dữ liệu chi tiết
    }
}
