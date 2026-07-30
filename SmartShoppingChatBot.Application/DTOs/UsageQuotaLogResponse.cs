using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs;

public class UsageQuotaLogResponse
{
    public string Id { get; set; } = string.Empty;

    public string BusinessQuotaId { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public SourceTypeEnum SourceType { get; set; }

    public long InputTokens { get; set; }

    public long OutputTokens { get; set; }

    public long BillableTokens { get; set; }

    public int MessageUsed { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
