using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class BusinessSubscription
    {
        [Key]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId Id { get; set; }
        public ObjectId BusinessId { get; set; }
        public ObjectId SubscriptionPlanId { get; set; }
        public DateTimeOffset StartDate { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset EndDate { get; set; }
        [BsonRepresentation(BsonType.String)]
        public StatusEnums Status { get; set; } = StatusEnums.Inactive;
    }
}
