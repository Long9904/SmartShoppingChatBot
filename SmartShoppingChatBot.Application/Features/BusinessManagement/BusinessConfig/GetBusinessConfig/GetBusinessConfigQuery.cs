using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.GetBusinessConfig;

public class GetBusinessConfigQuery : IRequest<Result<BusinessConfigResponse>>
{
}
