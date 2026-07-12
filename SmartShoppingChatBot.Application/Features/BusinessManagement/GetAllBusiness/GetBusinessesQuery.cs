using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.GetAllBusiness;

public class GetBusinessesQuery : IRequest<Result<BasePaginatedList<BusinessResponse>>>
{
    public GetBusinessesFilter Filter { get; set; } = new();
}
