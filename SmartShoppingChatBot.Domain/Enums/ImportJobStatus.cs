namespace SmartShoppingChatBot.Domain.Enums
{
    public enum ImportJobStatus
    {
        Pending,
        Validating,
        ImportingProducts,
        Completed,
        CompletedWithErrors,
        Failed
    }
}
