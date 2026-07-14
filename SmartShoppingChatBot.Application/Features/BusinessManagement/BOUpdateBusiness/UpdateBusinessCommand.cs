using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.BOUpdateBusiness;

public class UpdateBusinessCommand : IRequest<Result<BusinessResponse>>
{
    public string BusinessName { get; init; } = string.Empty;
    public string HotLine { get; init; } = string.Empty;
    public string WebsiteUrl { get; init; } = string.Empty;
    public string AddressLine { get; init; } = string.Empty;
}
