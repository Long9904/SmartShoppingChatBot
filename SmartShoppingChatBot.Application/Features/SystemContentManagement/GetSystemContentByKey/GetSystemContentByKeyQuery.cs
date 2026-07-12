using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.SystemContentManagement.GetSystemContentByKey;

public class GetSystemContentByKeyQuery : IRequest<Result<SystemContentResponse>>
{
    public string Key { get; set; } = string.Empty;
}
