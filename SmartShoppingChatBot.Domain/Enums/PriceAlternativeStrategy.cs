using System.Text.Json.Serialization;

namespace SmartShoppingChatBot.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PriceAlternativeStrategy
{
    DownSell = 0,
    UpSell = 1
}
