using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Domain.Entities;

public class CategoryAttributeSchema
{
    [Key]
    public ObjectId Id { get; set; }

    public string Category { get; set; } = default!;   // "thời trang > áo nam", khớp field Category của Product
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = false;         // chưa duyệt thì chưa dùng được
    public List<AttributeDefinition> Attributes { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public UserEmbedded CreatedBy { get; set; } = default!;
    public UserEmbedded? ApprovedBy { get; set; }        // ai duyệt, khi nào
    public DateTimeOffset? ApprovedAt { get; set; }
}


public class AttributeDefinition
{
    public string Key { get; set; } = default!;          // "color", "ram_gb" — chuẩn hóa, không tiếng Việt
    public string DisplayName { get; set; } = default!;  // "Màu sắc" — hiển thị UI
    public AttributeDataType DataType { get; set; }       // Keyword / Number / Boolean
    public bool IsFilterable { get; set; } = true;        // Có thể filter ko ?
    public bool IsStrict { get; set; } = false;           // true = lọc cứng, false = chỉ gợi ý semantic
    public List<string>? AllowedValues { get; set; }      // dùng cho Keyword: canonical values
}
