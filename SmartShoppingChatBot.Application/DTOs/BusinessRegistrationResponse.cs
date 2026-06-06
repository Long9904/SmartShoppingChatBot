using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public class BusinessRegistrationResponse
{
    public string? Id { get; set; }
    public string? BusinessName { get; set; }
    public string? Email { get; set; }
    public string? HotLine { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? BrandAssetsUrl { get; set; }
    public BusinessEnums? BusinessStatus { get; set; }

}
