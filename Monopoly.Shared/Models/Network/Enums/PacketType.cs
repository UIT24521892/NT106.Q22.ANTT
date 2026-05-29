using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Models.Network.Enums
{
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
}
