using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;

public static class ProductVariantAttributes
{
    public static bool TryGetCanonicalKeyword(
        Product product,
        AttributeDefinition definition,
        out string? canonicalValue)
    {
        canonicalValue = null;
        if (definition.DataType != AttributeDataType.Keyword
            || (!string.Equals(definition.Key, "color", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(definition.Key, "size", StringComparison.OrdinalIgnoreCase)))
            return false;

        var source = product.Metadata.FirstOrDefault(item =>
            string.Equals(item.Key, definition.Key, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(definition.Key, "color", StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Key, "COLOR EX", StringComparison.OrdinalIgnoreCase)));

        if (source.Key is null || string.IsNullOrWhiteSpace(source.Value))
            return false;

        var rawValue = source.Value.Trim();
        if (definition.AllowedValues is not { Count: > 0 })
        {
            canonicalValue = rawValue;
            return true;
        }

        var normalized = string.Join('_', rawValue.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        canonicalValue = definition.AllowedValues.FirstOrDefault(allowed =>
            string.Equals(allowed, rawValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(allowed, normalized, StringComparison.OrdinalIgnoreCase));
        return true;
    }
}
