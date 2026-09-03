using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ActivityLogResponse
    {
        public string Id { get; set; } = string.Empty;
        public string? BusinessId { get; set; }
        public string? ActorId { get; set; }
        public string? ActorEmail { get; set; }
        public RoleEnums? ActorRole { get; set; }
        public ActionLogEnums Action { get; set; }
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public StatusLogEnums Status { get; set; }
        public SeverityLogEnums Severity { get; set; }
        public string? Description { get; set; }
        public string? IpAddress { get; set; }
        public object? Metadata { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
