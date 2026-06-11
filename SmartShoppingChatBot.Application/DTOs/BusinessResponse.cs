using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public class BusinessResponse
{
    public string? Id { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string HotLine { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public BusinessEnums BusinessStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
