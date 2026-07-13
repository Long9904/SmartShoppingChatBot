using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System.Security.Cryptography;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.CreateNewKey
{
    public class CreateNewKeyCommandHandler : IRequestHandler<CreateNewKeyCommand, Result<CreateApiKeyResponse>>
    {

        private readonly IHashService _hashService;
        private readonly IApiKeyRepository _apiKeyRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly TimeProvider _time;

        public CreateNewKeyCommandHandler(
            IHashService hashService,
            IApiKeyRepository apiKeyRepository,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            TimeProvider time)
        {
            _hashService = hashService;
            _apiKeyRepository = apiKeyRepository;
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _time = time;
        }

        public async Task<Result<CreateApiKeyResponse>> Handle(CreateNewKeyCommand request, CancellationToken cancellationToken)
        {

            var user = await _currentUserService.GetUser();

            if (!user.IsSuccess)
            {
                return Result<CreateApiKeyResponse>.Failure(user.StatusCode, user.Message);
            }

            var business = await _currentUserService.GetBusiness();

            if (!business.IsSuccess)
            {
                return Result<CreateApiKeyResponse>.Failure(business.StatusCode, business.Message);
            }

            // Create a new API key
            var keyId = "ssc_live_" + RandomHex(6);
            var secret = RandomBase64Url(48);
            var fullKey = $"{keyId}.{secret}";

            var hashKey = _hashService.HmacSha256(secret);
            var encryptedSecret = _hashService.Encrypt(secret);

            var dateNow = _time.GetUtcNow();

            var apiKey = new ApiKey
            {
                Id = ObjectId.GenerateNewId(),
                Name = request.Name,
                KeyId = keyId,
                HashKey = hashKey,
                EncryptedSecret = encryptedSecret,
                BusinessId = business.Data.Id,
                Status = KeyStatus.Active,
                CreatedAt = dateNow,
                UpdatedAt = dateNow,
                CreatedBy = new UserEmbedded
                {
                    Id = user.Data.Id,
                    Name = user.Data.FullName,
                },

                UpdatedBy = new UserEmbedded
                {
                    Id = user.Data.Id,
                    Name = user.Data.FullName,
                }
            };

            await _apiKeyRepository.AddAsync(apiKey);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var response = new CreateApiKeyResponse
            {
                Id = apiKey.Id.ToString(),
                Name = apiKey.Name,
                KeyId = apiKey.KeyId,
                FullKey = fullKey,
                Status = apiKey.Status,
                CreatedAt = apiKey.CreatedAt,
            };

            return Result<CreateApiKeyResponse>.Success(response, 201, "API key tạo thành công", "MG_APIKEY_201");
        }


        private static string RandomHex(int byteLength)
        {
            var bytes = RandomNumberGenerator.GetBytes(byteLength);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private static string RandomBase64Url(int byteLength)
        {
            var bytes = RandomNumberGenerator.GetBytes(byteLength);

            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
