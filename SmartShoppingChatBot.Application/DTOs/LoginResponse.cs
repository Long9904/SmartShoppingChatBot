namespace SmartShoppingChatBot.Application.DTOs;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public bool IsEmailVerified { get; set; }
    public bool IsProfileCompleted { get; set; }
}

