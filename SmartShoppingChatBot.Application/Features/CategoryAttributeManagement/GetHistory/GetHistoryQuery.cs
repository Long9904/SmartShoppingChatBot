using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetHistory;

public sealed class GetHistoryQuery : IRequest<Result<List<CategoryAttributeSchemaResponse>>>
{
    public string Category { get; set; } = string.Empty;
}
