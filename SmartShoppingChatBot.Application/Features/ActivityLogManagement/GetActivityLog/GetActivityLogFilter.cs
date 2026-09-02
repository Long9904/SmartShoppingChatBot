using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.ActivityLogManagement.GetActivityLog
{
    public class GetActivityLogFilter : QueryBase
    {
        public string? BusinessId { get; set; }     
        public string? Keyword { get; set; }       
        public string? ActorId { get; set; }        
        public ActionLogEnums? Action { get; set; }
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public StatusLogEnums? Status { get; set; }
        public SeverityLogEnums? Severity { get; set; }
        public DateTimeOffset? FromDate { get; set; }
        public DateTimeOffset? ToDate { get; set; }
    }
}
