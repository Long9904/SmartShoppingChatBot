using System.Text.Json.Serialization;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.SendMessage
{
    public class SendMessageCommand : IRequest<Result<ConversationResponse>>
    {
        [JsonIgnore]
        public string ConversationId { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string ExternalCustomerId { get; set; } = string.Empty;
    }
}
