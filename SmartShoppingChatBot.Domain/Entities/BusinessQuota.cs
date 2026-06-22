using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class BusinessQuota
    {
        [Key]
        [BsonRepresentation(BsonType.ObjectId)]
        public ObjectId Id { get; set; }
        public ObjectId BusinessId { get; set; }
        public ObjectId BusinessSubscriptionId { get; set; }
        // Snapshot for quota 
        public long TokenLimit { get; set; }
        public int MessageLimit { get; set; }
        // Usage
        public long UsedTokens { get; set; }
        public int UsedMessages { get; set; }
        // Reset quota day
        public DateTimeOffset ResetDate { get; set; }

    }
}
