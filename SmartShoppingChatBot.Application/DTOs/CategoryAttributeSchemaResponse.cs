using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.DTOs;

public sealed class CategoryAttributeSchemaResponse
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public List<AttributeDefinition> Attributes { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public CategoryAttributeSchemaUserResponse CreatedBy { get; set; } = default!;
    public CategoryAttributeSchemaUserResponse? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

public sealed class CategoryAttributeSchemaUserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
