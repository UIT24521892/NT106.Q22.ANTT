using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Models.Network.Payloads.Auth
{
    public class AuthPayload
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
}
