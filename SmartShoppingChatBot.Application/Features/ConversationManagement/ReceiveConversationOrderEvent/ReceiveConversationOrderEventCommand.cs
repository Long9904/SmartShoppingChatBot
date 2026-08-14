using System.Text.Json.Serialization;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.ReceiveConversationOrderEvent;

public sealed class ReceiveConversationOrderEventCommand : IRequest<Result<ConversationOrderEventResponse>>
{
    [JsonIgnore]
    public string ConversationId { get; set; } = string.Empty;

    public ConversationOrderEventRequest Event { get; init; } = new();
}
