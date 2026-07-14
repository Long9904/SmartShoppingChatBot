using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.SystemContentManagement.GetAllSystemContent
{
    public class GetAllSystemContentQuery : IRequest<Result<BasePaginatedList<object>>>
    {
        public GetAllSystemContentFilter Filter { get; set; } = new GetAllSystemContentFilter();
    }
}
