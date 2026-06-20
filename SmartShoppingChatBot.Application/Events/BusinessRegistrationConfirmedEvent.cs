using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Events;

public class BusinessRegistrationConfirmedEvent
{
    public string? BusinessId { get; set; }

    public string? BusinessName { get; set; }

    public string? OwnerEmail { get; set; }

    public string? OwnerName { get; set; }

    public string? TokenVerification { get; set; }

    public BusinessEnums BusinessStatus { get; set; }
}
