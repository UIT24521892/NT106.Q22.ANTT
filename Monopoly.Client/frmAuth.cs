using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Monopoly.Client
{
    public class frmAuth : Form
    {
        private bool isLoginMode = true;
        private Label lblTitle;
        private TextBox txtEmail, txtPassword;
        private Button btnAction, btnSwitch;

        public frmAuth()
        {
            this.Size = new Size(350, 400);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "Cổng Xác Thực";

            lblTitle = new Label { Text = "ĐĂNG NHẬP", Font = new Font("Arial", 16, FontStyle.Bold), 
                Location = new Point(100, 30), AutoSize = true };

            Label lblEmail = new Label { Text = "Email:", Location = new Point(50, 90), 
                AutoSize = true };
            txtEmail = new TextBox { Location = new Point(50, 110), Width = 230 };

            Label lblPass = new Label { Text = "Mật khẩu:", Location = new Point(50, 160), 
                AutoSize = true };
            txtPassword = new TextBox { Location = new Point(50, 180), Width = 230, 
                PasswordChar = '*' };

            btnAction = new Button { Text = "ĐĂNG NHẬP", Location = new Point(50, 240), Width = 230, 
                Height = 40, BackColor = Color.Teal, ForeColor = Color.White };
            btnSwitch = new Button { Text = "Chưa có tài khoản? Đăng ký ngay", 
                Location = new Point(50, 290), Width = 230 };

            btnAction.Click += BtnAction_Click;
            btnSwitch.Click += BtnSwitch_Click;

            this.Controls.AddRange(new Control[] { lblTitle, lblEmail, txtEmail, lblPass, 
                txtPassword, btnAction, btnSwitch });
        }

        private void BtnSwitch_Click(object sender, EventArgs e)
        {
            isLoginMode = !isLoginMode;
            lblTitle.Text = isLoginMode ? "ĐĂNG NHẬP" : "ĐĂNG KÝ TÀI KHOẢN";
            btnAction.Text = isLoginMode ? "ĐĂNG NHẬP" : "TẠO TÀI KHOẢN";
            btnSwitch.Text = isLoginMode ? "Chưa có tài khoản? Đăng ký ngay" : 
                "Đã có tài khoản? Đăng nhập";
        }

        private async void BtnAction_Click(object sender, EventArgs e)
        {
            // 1. Tạo cục JSON gửi đi
            var payload = new { Email = txtEmail.Text, Password = txtPassword.Text };
            var packet = new
            {
                Type = isLoginMode ? "Login" : "Register",
                Payload = JsonConvert.SerializeObject(payload)
            };

            string jsonToSend = JsonConvert.SerializeObject(packet) + "<EOF>"; // Dấu kết thúc tin nhắn
            byte[] outStream = Encoding.UTF8.GetBytes(jsonToSend);

            // 2. Bắn qua TCP Server
            await frmSplash.ServerStream.WriteAsync(outStream, 0, outStream.Length);

            // Note: Cần viết thêm luồng (Thread) Lắng nghe kết quả trả về từ Server để hiển thị MessageBox.
        }
    }
}