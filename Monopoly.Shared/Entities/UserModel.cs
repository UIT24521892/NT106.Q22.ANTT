using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Monopoly.Shared.Entities
{
    public class UserModel
    {
        public string UserID { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public int Point { get; set; }
        public int TotalWins { get; set; }
        public int TotalLosses { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
