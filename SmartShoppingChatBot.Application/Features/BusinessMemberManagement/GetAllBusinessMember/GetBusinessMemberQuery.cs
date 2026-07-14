using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.BusinessMemberManagement.GetAllBusinessMember
{
    public class GetBusinessMemberQuery : IRequest<Result<BasePaginatedList<object>>>
    {
        public GetBusinessMemberFilter Filter { get; set; } = new();
    }
}
