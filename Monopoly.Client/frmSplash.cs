using System;
using System.Drawing;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Monopoly.Client
{
    public class frmSplash : Form
    {
        private Label lblStatus;
        public static TcpClient ClientSocket;
        public static NetworkStream ServerStream;

        public frmSplash()
        {
            this.Size = new Size(400, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.None; // Xóa viền để làm Splash Screen
            this.BackColor = Color.DarkSlateBlue;

            Label lblTitle = new Label { Text = "MONOPOLY GAME", Font = new Font("Arial", 20, FontStyle.Bold), ForeColor = Color.White, Location = new Point(70, 50), AutoSize = true };
            lblStatus = new Label { Text = "Đang kết nối tới Máy chủ TCP...", ForeColor = Color.LightGray, Location = new Point(100, 100), AutoSize = true };

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblStatus);
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await ConnectToServer();
        }

        private async Task ConnectToServer()
        {
            try
            {
                ClientSocket = new TcpClient();
                // Kết nối tới Server đang mở ở máy tính của bạn (Localhost) qua cổng 8080
                await ClientSocket.ConnectAsync("127.0.0.1", 8080);
                ServerStream = ClientSocket.GetStream();

                lblStatus.Text = "Kết nối thành công! Đang tải hệ thống...";
                await Task.Delay(1000); // Dừng 1 giây cho đẹp mắt

                // Chuyển sang Form Đăng nhập
                this.Hide();
                frmAuth authForm = new frmAuth();
                authForm.ShowDialog();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Máy chủ chưa mở hoặc bị lỗi mạng.\nChi tiết: {ex.Message}");
                Application.Exit();
            }
        }
    }
}