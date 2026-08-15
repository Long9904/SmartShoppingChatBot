using System.Text.Json.Serialization;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.UpdateConversationOrderStatus;

public sealed class UpdateConversationOrderStatusCommand : IRequest<Result<ConversationOrderResponse>>
{
    [JsonIgnore]
    public string ExternalOrderId { get; set; } = string.Empty;

    public ConversationOrderEventStatus Status { get; init; }
}
