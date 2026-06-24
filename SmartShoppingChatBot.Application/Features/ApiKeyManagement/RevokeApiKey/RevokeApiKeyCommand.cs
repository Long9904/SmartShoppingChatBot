using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.RevokeApiKey
{
    public class RevokeApiKeyCommand : IRequest<Result<string>>
    {
        public string Id { get; set; } = string.Empty;
    }
}
