using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.GetAllApiKey
{
    public class GetAllApiKeyQueryHandler : IRequestHandler<GetAllApiKeyQuery, Result<BasePaginatedList<ApiKeyResponse>>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IApiKeyRepository _apiKeyRepository;

        public GetAllApiKeyQueryHandler(
            ICurrentUserService currentUserService,
            IApiKeyRepository apiKeyRepository)
        {
            _currentUserService = currentUserService;
            _apiKeyRepository = apiKeyRepository;
        }

        public async Task<Result<BasePaginatedList<ApiKeyResponse>>> Handle(GetAllApiKeyQuery request, CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();

            if (!business.IsSuccess)
            {
                return Result<BasePaginatedList<ApiKeyResponse>>.Failure(business.StatusCode, business.Message, business.Errors);
            }

            var query = _apiKeyRepository.AsQueryable().Where(x =>
            x.BusinessId == business.Data.Id
            && x.Status == KeyStatus.Active);

            query = query.OrderByDescending(x => x.CreatedAt);

            var listApiKeys = await _apiKeyRepository.PaginatedListAsync(query, request.PageIndex, request.PageSize);

            var response = new BasePaginatedList<ApiKeyResponse>
            {
                Items = listApiKeys.Items.Select(x => new ApiKeyResponse
                {
                    Id = x.Id.ToString(),
                    Name = x.Name,
                    KeyId = x.KeyId,
                    MaskedKey = x.KeyId + "************",
                    Status = x.Status,
                    CreatedAt = x.CreatedAt
                }).ToList(),
                TotalItems = listApiKeys.TotalItems,
                PageIndex = listApiKeys.PageIndex,
                TotalPages = listApiKeys.TotalPages,
                PageSize = listApiKeys.PageSize
            };

            return Result<BasePaginatedList<ApiKeyResponse>>.Success(response, 200, "Get all api keys successfully");
        }
    }
}
