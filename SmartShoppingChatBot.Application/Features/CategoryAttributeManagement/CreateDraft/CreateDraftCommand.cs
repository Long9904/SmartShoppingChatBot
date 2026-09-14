using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.CreateDraft;

public sealed class CreateDraftCommand : IRequest<Result<CategoryAttributeSchemaResponse>>
{
    public string Category { get; set; } = string.Empty;
    public List<AttributeDefinition> Attributes { get; set; } = [];
}
