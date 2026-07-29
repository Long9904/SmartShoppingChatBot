using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.ResetBusinessConfig;

public class ResetBusinessConfigCommand : IRequest<Result<BusinessConfigResponse>>
{
}
