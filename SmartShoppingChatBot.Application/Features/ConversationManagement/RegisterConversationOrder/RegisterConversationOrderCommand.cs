using System.Text.Json.Serialization;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.RegisterConversationOrder;

public sealed class RegisterConversationOrderCommand : IRequest<Result<ConversationOrderResponse>>
{
    [JsonIgnore]
    public string ConversationId { get; set; } = string.Empty;

    public RegisterConversationOrderRequest Order { get; init; } = new();
}
