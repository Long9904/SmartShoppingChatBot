using MongoDB.Bson;

namespace SmartShoppingChatBot.Domain.Commons;

public class UserEmbedded
{
    public ObjectId Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
