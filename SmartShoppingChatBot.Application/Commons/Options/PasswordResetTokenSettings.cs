namespace SmartShoppingChatBot.Application.Commons.Options;

public class PasswordResetTokenSettings
{
    public int ExpireMinutes { get; set; }

    public string UrlBase { get; set; } = null!;
}
