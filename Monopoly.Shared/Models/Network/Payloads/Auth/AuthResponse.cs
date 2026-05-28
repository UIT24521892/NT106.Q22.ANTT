using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Models.Network.Payloads.Auth
{
    public class AuthResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }

        public string UID { get; set; }

        // ĐÂY LÀ NƠI NHẬN VÀ LƯU TRỮ JWT TOKEN TỪ FIREBASE AUTH
        public string JwtToken { get; set; }
    }
}
