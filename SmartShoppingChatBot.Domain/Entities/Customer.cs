using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities
{
    public class Customer
    {
        public ObjectId Id { get; set; }

        public required string CustomerExternalId { get; set; }

        public ObjectId BusinessId { get; set; }

        public string? Name { get; set; }

        public string? Email { get; set; }

        public string? Phone { get; set; }

        public CustomerStatus Status { get; set; } = CustomerStatus.Active;

        public DateTimeOffset? CreatedAt { get; set; }

        public DateTimeOffset? UpdatedAt { get;set; }

        public Dictionary<string, string> PersonalData { get; set; } = [];
    }
}
