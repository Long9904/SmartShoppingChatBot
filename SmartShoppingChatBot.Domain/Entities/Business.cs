using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities;

public class Business
{
    [Key]
    public ObjectId Id { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string HotLine { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string BrandAssetsUrl { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public BusinessEnums BusinessStatus { get; set; } = BusinessEnums.PENDING;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
    public UserEmbedded? CreatedBy { get; set; }
    public UserEmbedded? UpdatedBy { get; set; }
}
