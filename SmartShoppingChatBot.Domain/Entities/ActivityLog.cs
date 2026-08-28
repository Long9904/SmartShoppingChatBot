using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class ActivityLog
    {
        public ObjectId Id { get; set; }
        public string? BusinessId { get; set; }
        public string? ActorId { get; set; }
        public string? ActorEmail { get; set; }
        public RoleEnums? ActorRole { get; set; }

        public ActionLogEnums Action { get; set; }

        public string? TargetType { get; set; }
        public string? TargetId { get; set; }

        public StatusLogEnums Status { get; set; } = StatusLogEnums.Success;
        public SeverityLogEnums Severity { get; set; } = SeverityLogEnums.Info;

        public string? Description { get; set; }
        public string? IpAddress { get; set; }

        public string? MetadataJson { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
