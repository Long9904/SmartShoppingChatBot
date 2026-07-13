using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Events
{
    public class PaymentCompletedEvent
    {
        public string PaymentId { get; set; } = string.Empty;
    }
}
