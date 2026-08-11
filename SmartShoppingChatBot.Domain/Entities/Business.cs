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

    public BusinessConfig? Config { get; set; }
}


public class BusinessConfig
{
    public double? ModelTemperature { get; set; } = 0.2;

    public int? TopKDocument { get; set; } = 3;

    public double? RerankingScore { get; set; } = 0.75;

    public string? SystemPrompt { get; set; } = string.Empty;

    public string? FallBackMessage { get; set; } = string.Empty;

    public int? MaxOutPutToken { get; set; } = 2000;
}
