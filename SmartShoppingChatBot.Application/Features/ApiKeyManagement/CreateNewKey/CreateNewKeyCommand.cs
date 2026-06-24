using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.CreateNewKey
{
    public class CreateNewKeyCommand : IRequest<Result<CreateApiKeyResponse>>
    {
        public string Name { get; set; } = string.Empty;
    }
}
