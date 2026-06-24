using MediatR;
using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.GetAllApiKey
{
    public class GetAllApiKeyQuery : QueryBase, IRequest<Result<BasePaginatedList<ApiKeyResponse>>>
    {
    }
}
