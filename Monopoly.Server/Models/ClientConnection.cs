using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Server.Models
{
    public class ClientConnection
    {
        public TcpClient TcpClient { get; set; }
        public NetworkStream Stream { get; set; }
        public string Uid { get; set; }
        public string Username { get; set; }
        public string IdToken { get; set; }
        public string CurrentRoomId { get; set; }
    }
}
