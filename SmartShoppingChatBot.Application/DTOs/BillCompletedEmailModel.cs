namespace SmartShoppingChatBot.Application.DTOs
{
    public class BillCompletedEmailModel
    {
        public string BusinessName { get; set; } = string.Empty;
        public string InvoiceId { get; set; } = string.Empty;
        public long OrderCode { get; set; }
        public decimal Amount { get; set; }
        public DateTimeOffset PaidAt { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string SubscriptionName { get; set; } = string.Empty;
        public DateTimeOffset SubscriptionStartDate { get; set; }
        public DateTimeOffset SubscriptionEndDate { get; set; }
        public string InvoiceUrl { get; set; } = string.Empty;
        public string SupportEmail { get; set; } = string.Empty;
    }
}
