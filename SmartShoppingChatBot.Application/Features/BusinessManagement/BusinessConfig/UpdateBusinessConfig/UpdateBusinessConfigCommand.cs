using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.UpdateBusinessConfig;

public class UpdateBusinessConfigCommand : IRequest<Result<BusinessConfigResponse>>
{
    public double? ModelTemperature { get; set; }

    public int? TopKDocument { get; set; }

    public double? RerankingScore { get; set; }

    public string? SystemPrompt { get; set; }

    public string? FallBackMessage { get; set; }

    public int? MaxOutPutToken { get; set; }
}
