using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public class LoginResponse
{
    public string TempToken { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsProfileCompleted { get; set; }

    public List<BusinessLoginResponse> Businesses { get; set; } = new List<BusinessLoginResponse>();
}

public class BusinessLoginResponse
{
    public string Id { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public RoleEnums Role { get; set; }
}
