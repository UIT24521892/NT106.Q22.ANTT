using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Monopoly.Server.Network;
using Monopoly.Server.GameLogic;

namespace Monopoly.Server
{
    class Program
    {
        public const int TurnDurationSeconds = 45;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            Console.Title = "Monopoly Game Server - Nhóm 4";
            Console.WriteLine("=====================================");
            Console.WriteLine("    MÁY CHỦ CỜ TỶ PHÚ ĐANG KHỞI ĐỘNG ");
            Console.WriteLine("=====================================");

            TcpListener listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();

            Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - Server đang lắng nghe tại cổng 8080...\n");

            _ = TurnTimer.RunTurnTimerLoopAsync();

            while (true)
            {
                TcpClient tcpClient = await listener.AcceptTcpClientAsync();

                Console.WriteLine($"[CONNECT] Client mới: {tcpClient.Client.RemoteEndPoint}");

                // Không await ở đây để server tiếp tục nhận client khác
                _ = TcpServer.HandleClientAsync(tcpClient);
            }
        }
    }
}
