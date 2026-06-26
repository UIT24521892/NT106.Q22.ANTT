using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Monopoly.Server.Network;
using Monopoly.Server.GameLogic;
using System.Linq;

namespace Monopoly.Server
{
    class Program
    {
        public const int TurnDurationSeconds = 45;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            if (args.Contains("--run-tests"))
            {
                GameEngineTests.Run();
                return;
            }

            Console.Title = "Monopoly Game Server - Nhóm 4";
            Console.WriteLine("=====================================");
            Console.WriteLine("    MÁY CHỦ CỜ TỶ PHÚ ĐANG KHỞI ĐỘNG ");
            Console.WriteLine("=====================================");

            int port = ResolvePort(args);
            TcpListener listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} - Server đang lắng nghe tại cổng {port}...\n");

            Console.WriteLine($"[NETWORK] TCP endpoint: 0.0.0.0:{port}");
            Console.WriteLine("[NETWORK] Public access requires firewall/NAT/VPS port exposure.");

            _ = TurnTimer.RunTurnTimerLoopAsync();

            while (true)
            {
                TcpClient tcpClient = await listener.AcceptTcpClientAsync();

                Console.WriteLine($"[CONNECT] Client mới: {tcpClient.Client.RemoteEndPoint}");

                _ = TcpServer.HandleClientAsync(tcpClient);
            }
        }

        private static int ResolvePort(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i].Equals("--port", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(args[i + 1], out int argumentPort) &&
                    argumentPort >= 1 &&
                    argumentPort <= 65535)
                {
                    return argumentPort;
                }
            }

            string environmentPort = Environment.GetEnvironmentVariable("MONOPOLY_PORT");
            if (int.TryParse(environmentPort, out int environmentValue) &&
                environmentValue >= 1 &&
                environmentValue <= 65535)
            {
                return environmentValue;
            }

            return 8080;
        }
    }
}
