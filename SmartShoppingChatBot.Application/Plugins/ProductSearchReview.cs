namespace SmartShoppingChatBot.Application.Plugins;

public sealed class ProductSearchReview
{
    public List<string> NextFunctions { get; init; } = [];
    public bool CanConclude { get; init; }
    public string Instruction { get; init; } = string.Empty;
}
