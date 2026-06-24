using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace SmartShoppingChatBot.Domain.Entities;

public class Business
{
    [Key]
    public ObjectId Id { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string HotLine { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public BusinessEnums BusinessStatus { get; set; } = BusinessEnums.PENDING_APPROVAL;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public UserEmbedded? CreatedBy { get; set; }
    public UserEmbedded? UpdatedBy { get; set; }
}
