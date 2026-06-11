using System.ComponentModel.DataAnnotations;

namespace SmartShoppingChatBot.Application.Commons.Options;

public class JwtSettings
{
    public string SecretKey { get; set; } = default!;
    [Required] public string Issuer { get; set; } = default!;
    [Required] public string Audience { get; set; } = default!;
    [Range(1, int.MaxValue)] public int ExpireMinutes { get; set; }

    public int TempTokenExpireMinutes { get; set; }
}
