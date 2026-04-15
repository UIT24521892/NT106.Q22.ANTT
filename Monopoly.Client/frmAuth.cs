using Monopoly.Shared;
using Monopoly.Shared;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Monopoly.Client
{
    public class frmAuth : Form
    {
        // Khai báo các Controls
        private Label lblTitle;
        private TextBox txtEmail;
        private TextBox txtPassword;
        private Button btnLogin;
        private Button btnRegister;
        private LinkLabel lnkForgotPass;

        public frmAuth()
        {
            InitializeComponentProgrammatically();
        }

        private void InitializeComponentProgrammatically()
        {
            // 1. Cấu hình Form
            this.Text = "Monopoly - Đăng Nhập";
            this.Size = new Size(350, 450);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.WhiteSmoke;

            // 2. Khởi tạo Controls
            lblTitle = new Label { Text = "CỜ TỶ PHÚ", Font = new Font("Arial", 20, FontStyle.Bold), ForeColor = Color.DarkRed, Location = new Point(90, 30), Size = new Size(200, 40) };

            Label lblEmail = new Label { Text = "Email:", Location = new Point(50, 100), Size = new Size(250, 20) };
            txtEmail = new TextBox { Location = new Point(50, 120), Size = new Size(230, 30), Font = new Font("Arial", 12) };

            Label lblPass = new Label { Text = "Mật khẩu:", Location = new Point(50, 170), Size = new Size(250, 20) };
            txtPassword = new TextBox { Location = new Point(50, 190), Size = new Size(230, 30), Font = new Font("Arial", 12), PasswordChar = '*' };

            btnLogin = new Button { Text = "ĐĂNG NHẬP", Location = new Point(50, 250), Size = new Size(230, 40), BackColor = Color.Teal, ForeColor = Color.White, Font = new Font("Arial", 10, FontStyle.Bold), FlatStyle = FlatStyle.Flat };
            btnRegister = new Button { Text = "Tạo tài khoản mới", Location = new Point(50, 300), Size = new Size(230, 30), FlatStyle = FlatStyle.Flat };
            lnkForgotPass = new LinkLabel { Text = "Quên mật khẩu?", Location = new Point(120, 340), Size = new Size(150, 20) };

            // 3. Đăng ký Sự kiện (Events)
            btnLogin.Click += BtnLogin_Click;
            btnRegister.Click += BtnRegister_Click;
            lnkForgotPass.Click += LnkForgotPass_Click;

            // 4. Gắn Controls vào Form
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblEmail);
            this.Controls.Add(txtEmail);
            this.Controls.Add(lblPass);
            this.Controls.Add(txtPassword);
            this.Controls.Add(btnLogin);
            this.Controls.Add(btnRegister);
            this.Controls.Add(lnkForgotPass);
        }

        // --- XỬ LÝ LOGIC NÚT BẤM ---

        private void BtnLogin_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtEmail.Text) || string.IsNullOrEmpty(txtPassword.Text))
            {
                MessageBox.Show("Vui lòng nhập đủ Email và Mật khẩu!");
                return;
            }

            // Đóng gói dữ liệu
            var payload = new AuthPayload { Email = txtEmail.Text, Password = txtPassword.Text };
            var packet = new NetworkPacket
            {
                Type = PacketType.Login,
                Payload = JsonConvert.SerializeObject(payload)
            };

            string jsonToSend = JsonConvert.SerializeObject(packet);

            // TƯỞNG TƯỢNG HÀM NÀY: TcpClientManager.Instance.Send(jsonToSend);
            MessageBox.Show($"Đang gửi qua TCP:\n{jsonToSend}");
        }

        private void BtnRegister_Click(object sender, EventArgs e)
        {
            // Tương tự Login, đổi PacketType thành Register
        }

        private void LnkForgotPass_Click(object sender, EventArgs e)
        {
            // Dùng InputBox hoặc form nhỏ để nhập email, sau đó gửi PacketType.ResetPassword
        }
    }
}