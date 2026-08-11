namespace SmartShoppingChatBot.Application.DTOs;

public class BusinessConfigResponse
{
    public double? ModelTemperature { get; set; }

    public int? TopKDocument { get; set; }

    public double? RerankingScore { get; set; }

    public string? SystemPrompt { get; set; }

    public string? FallBackMessage { get; set; }

    public int? MaxOutPutToken { get; set; }
}
