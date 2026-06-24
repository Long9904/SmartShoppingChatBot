using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.RevealKey
{
    public class RevealKeyQueryHandler : IRequestHandler<RevealKeyQuery, Result<string>>
    {
        private readonly IApiKeyRepository _apiKeyRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHashService _hashService;

        public RevealKeyQueryHandler(
            IApiKeyRepository apiKeyRepository, 
            ICurrentUserService currentUserService, 
            IHashService hashService)
        {
            _apiKeyRepository = apiKeyRepository;
            _currentUserService = currentUserService;
            _hashService = hashService;
        }

        public async Task<Result<string>> Handle(RevealKeyQuery request, CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();
            if (!business.IsSuccess)
            {
                return Result<string>.Failure(business.StatusCode, business.Message, business.Errors);
            }

            var apiKey = await _apiKeyRepository.FindAsync(k => k.KeyId == request.KeyId);

            if (apiKey == null) return Result<string>.Failure(404, "API key not found.");

            if (apiKey.BusinessId != business.Data.Id) 
                return Result<string>.Failure(403, "You do not have permission to reveal this API key.");

            if (apiKey.Status != KeyStatus.Active) 
                return Result<string>.Failure(400, "API key is not active.");

            var secret = _hashService.Decrypt(apiKey.EncryptedSecret);

            var fullKey = $"{apiKey.KeyId}.{secret}";

            return Result<string>.Success(fullKey, 200, "API key revealed successfully.");
        }
    }
}
