namespace SmartShoppingChatBot.Application.DTOs;

public class MyBusinessProfileResponse : BusinessResponse
{
    public CurrentBusinessSubscriptionResponse? CurrentSubscription { get; set; }
    public BusinessQuotaResponse? BusinessQuota { get; set; }
}

public class CurrentBusinessSubscriptionResponse
{
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
}

public class BusinessQuotaResponse
{
    public string Id { get; set; } = string.Empty;
    public string BusinessSubscriptionId { get; set; } = string.Empty;
    public long TokenLimit { get; set; }
    public int MessageLimit { get; set; }
    public long UsedTokens { get; set; }
    public int UsedMessages { get; set; }
    public int MaxProductAllowed { get; set; }
    public DateTimeOffset ResetDate { get; set; }
}
