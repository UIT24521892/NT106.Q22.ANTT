using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Models.Network
{
    // Phân loại các hành động gửi qua TCP
    public enum PacketType
    {
        //login action
        Login,
        Register,
        ResetPassword, // Bổ sung tính năng Quên mật khẩu
        Logout,        // Bổ sung tín hiệu Đăng xuất
        //play action
        DiceRoll,
        BuyProperty,
        EndTurn,
        UseCard
        // ... thêm các hành động khác
    }

    // Lớp vỏ bọc cho mọi tin nhắn TCP (Packet chuẩn)
    public class NetworkPacket
    {
        public PacketType Type { get; set; }
        public string Payload { get; set; } // Chứa chuỗi JSON của dữ liệu chi tiết
    }

    // Khuôn dữ liệu Client gửi lên lúc đăng nhập/đăng ký
    public class AuthPayload
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    // Khuôn dữ liệu Server trả về 
    public class AuthResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }

        public string UID { get; set; }

        // ĐÂY LÀ NƠI NHẬN VÀ LƯU TRỮ JWT TOKEN TỪ FIREBASE AUTH
        public string JwtToken { get; set; }
    }
}
