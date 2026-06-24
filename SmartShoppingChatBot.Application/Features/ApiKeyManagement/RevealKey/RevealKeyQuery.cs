using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.RevealKey
{
    public class RevealKeyQuery : IRequest<Result<string>>
    {
        public string KeyId { get; set; } = string.Empty;
    }
}
