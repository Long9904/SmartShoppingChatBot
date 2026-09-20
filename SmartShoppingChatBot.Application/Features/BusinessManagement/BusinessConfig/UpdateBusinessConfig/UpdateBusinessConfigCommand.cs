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

    public decimal? LowPriceMaxLimit { get; set; }

    public decimal? MediumPriceMinLimit { get; set; }

    public decimal? MediumPriceMaxLimit { get; set; }

    public decimal? HighPriceMinLimit { get; set; }
}
