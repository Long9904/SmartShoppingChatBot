using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public class BusinessRegistrationResponse
{
    public string? Id { get; set; }
    public string? BusinessName { get; set; }
    public BusinessEnums? BusinessStatus { get; set; }
}
