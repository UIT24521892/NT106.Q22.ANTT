using System;
namespace Monopoly.Shared
{
    public class Class1
    {

    }
    // Phân loại hành động
    public enum PacketType
    {
        Login,
        Register,
        ResetPassword,
        VerifyEmail,
        AuthResponse
    }

    // Cái vỏ bọc bên ngoài của mọi gói tin gửi qua TCP
    public class NetworkPacket
    {
        public PacketType Type { get; set; }
        public string Payload { get; set; } // Chứa chuỗi JSON của dữ liệu thực sự
    }

    // Khuôn dữ liệu gửi từ Client -> Server
    public class AuthPayload
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    // Khuôn kết quả Server trả về -> Client
    public class AuthResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string UID { get; set; } // Giữ UID nếu đăng nhập thành công
    }
}
