using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly IApiKeyRepository _apiKeyRepository;
        private readonly IBusinessRepository _businessUnitRepository;
        private readonly IHashService _hashService;

        public AuthService(
            IApiKeyRepository apiKeyRepository, 
            IHashService hashService, 
            IBusinessRepository businessUnitRepository)
        {
            _apiKeyRepository = apiKeyRepository;
            _hashService = hashService;
            _businessUnitRepository = businessUnitRepository;
        }

        public async Task<Result<Business?>> ValidateApiKeyAsync(string value)
        {
            string[] parts = value.Split('.');

            var hash = _hashService.HmacSha256(parts[1]);

            var apiKey = await _apiKeyRepository.FindAsync(x => x.HashKey == hash && x.Status == KeyStatus.Active);

            if (apiKey == null) return Result<Business?>.Failure(404, "API key not found", null, ApiKeyMessageCode.NotFound);

            var business = await _businessUnitRepository.FindAsync(b => b.Id == apiKey.BusinessId);

            if (business == null) return Result<Business?>.Failure(404, "Business not found", null, BusinessMessageCode.NotFound);

            return business.BusinessStatus switch
            {
                BusinessEnums.ACTIVE => Result<Business?>.Success(business, 200, "Get business success", BusinessMessageCode.Sucess),

                BusinessEnums.PENDING_APPROVAL => Result<Business?>.Failure(400, "Business is waiting to approve", messageCode: BusinessMessageCode.WattingApprove),

                BusinessEnums.REJECTED => Result<Business?>.Failure(400, "Business is rejected.", messageCode: BusinessMessageCode.IsRejected),

                BusinessEnums.DELETED => Result<Business?>.Failure(404, "Business not found.", null, BusinessMessageCode.NotFound),

                _ => Result<Business?>.Failure(401, "Token is invalid.", messageCode: AuthMessageCode.InvalidAuthentication)
            };

        }
    }
}
