using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class RejectBusinessOwnerEmailModel
    {
        public string? BusinessName { get; set; }
        public string? OwnerEmail { get; set; }
        public string? OwnerName { get; set; }
    }
}
