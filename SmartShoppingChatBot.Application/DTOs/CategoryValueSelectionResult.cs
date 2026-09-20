using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed class CategoryValueSelectionResult
{
    [JsonPropertyName("values")]
    [Description("Attribute key/value pairs supported by the product content. Omit attributes whose values cannot be determined.")]
    public required List<CategoryValueSelectionItem> Values { get; init; }

    [JsonIgnore]
    public long InputTokens { get; set; }

    [JsonIgnore]
    public long OutputTokens { get; set; }
}

public sealed class CategoryValueSelectionItem
{
    [JsonPropertyName("key")]
    [Description("An attribute key from the supplied category schema.")]
    public required string Key { get; init; }

    [JsonPropertyName("value")]
    [Description("The selected canonical value. For numbers use digits and a decimal point only; for booleans use true or false.")]
    public required string Value { get; init; }
}
