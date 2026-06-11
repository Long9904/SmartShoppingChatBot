namespace SmartShoppingChatBot.Application.Interface
{
    public interface ICurrentUserService
    {
        string? GetUserId();
        string? GetBusinessId();
    }
}
