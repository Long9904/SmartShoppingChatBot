using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.RevokeApiKey
{
    public class RevokeApiKeyCommandHandler : IRequestHandler<RevokeApiKeyCommand, Result<string>>
    {
        private readonly IApiKeyRepository _apiKeyRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _time;
        private readonly ILogger<RevokeApiKeyCommandHandler> _logger;

        public RevokeApiKeyCommandHandler(
            IApiKeyRepository apiKeyRepository, 
            ICurrentUserService currentUserService, 
            IUnitOfWork unitOfWork,
            TimeProvider time, 
            ILogger<RevokeApiKeyCommandHandler> logger)
        {
            _apiKeyRepository = apiKeyRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _time = time;
            _logger = logger;
        }

        public async Task<Result<string>> Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();
            
            if(!business.IsSuccess)
            {
                return Result<string>.Failure(business.StatusCode, business.Message);
            }

            var apiKey = await _apiKeyRepository.FindAsync(x => x.Id == ObjectId.Parse(request.Id));

            if (apiKey == null)
            {
                return Result<string>.Failure(404, "API key not found.");
            }

            if (apiKey.BusinessId != business.Data.Id)
            {
                return Result<string>.Failure(403, "You do not have permission to revoke this API key.");
            }

            if (apiKey.Status == KeyStatus.Revoked)
            {
                return Result<string>.Failure(400, "API key is already revoked.");
            }

            var user = await _currentUserService.GetUser();
            if (!user.IsSuccess)
            {
                return Result<string>.Failure(user.StatusCode, user.Message);
            }

            var dateNow = _time.GetUtcNow();

            apiKey.Status = KeyStatus.Revoked;
            apiKey.UpdatedAt = dateNow;
            apiKey.RevokedAt = dateNow;

            apiKey.UpdatedBy = new()
            {
                Id = user.Data.Id,
                Name = user.Data.FullName,
            };

            apiKey.RevokedBy = new()
            {
                Id = user.Data.Id,
                Name = user.Data.FullName,
            };

            await _apiKeyRepository.UpdateAsync(apiKey);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("API key with Key_ID {ApiKeyId} has been revoked by user {UserId}.", apiKey.KeyId, user.Data.Id);

            return Result<string>.Success(apiKey.KeyId.ToString(), 200, "API key revoked successfully.");
        }
    }
}
