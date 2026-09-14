using System.Text.Json.Serialization;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.UpdateAttributeDefinition;

public sealed class UpdateAttributeDefinitionCommand : IRequest<Result<CategoryAttributeSchemaResponse>>
{
    [JsonIgnore]
    public string Category { get; set; } = string.Empty;

    [JsonIgnore]
    public string AttributeKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;
    public bool IsFilterable { get; set; } = true;
    public bool IsStrict { get; set; }
    public List<string>? AllowedValues { get; set; }
}
