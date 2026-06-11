namespace SmartShoppingChatBot.Application.DTOs;

public class ApproveBusinessOwnerEmailModel
{
    public string? VerificationToken { get; set; }
    public string? BusinessName { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerName { get; set; }
}
