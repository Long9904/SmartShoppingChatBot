using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.GetAllBusinessMember
{
    public class GetBusinessMemberQueryHandler : IRequestHandler<GetBusinessMemberQuery, Result<BasePaginatedList<object>>>
    {
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetBusinessMemberQueryHandler(
            IUserRepository userRepository, 
            ICurrentUserService currentUserService, 
            IMapper mapper)
        {
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _mapper = mapper;   
        }

        public async Task<Result<BasePaginatedList<object>>> Handle(GetBusinessMemberQuery request, CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();
            if (!business.IsSuccess)
            {
                return Result<BasePaginatedList<object>>.Failure(business.StatusCode, business.Message);
            }

            var query =  _userRepository.AsQueryable();
            query = query.Where(x => x.Business.Id == business.Data.Id && x.Business.Role == RoleEnums.CATALOG_TEAM && x.UserStatus != UserStatus.DELETED);

            if (string.IsNullOrWhiteSpace(request.Filter.OrderBy))
            {
                request.Filter.OrderBy = "JoinedAt desc";
            }

            if (request.Filter.UserStatus != null)
            {
                query = query.Where(x => x.UserStatus == request.Filter.UserStatus.Value);
            }

            if (!string.IsNullOrEmpty(request.Filter.Email))
            {
                query = query.Where(x => x.Email.Contains(request.Filter.Email));
            }

            if (!string.IsNullOrEmpty(request.Filter.FullName))
            {
                query = query.Where(x => x.FullName.Contains(request.Filter.FullName));
            }

            if (request.Filter.IsEmailVerified.HasValue)
            {
                query = query.Where(x => x.IsEmailVerified == request.Filter.IsEmailVerified.Value);
            }

            if (request.Filter.Gender.HasValue)
            {
                query = query.Where(x => x.Gender == request.Filter.Gender.Value);
            }

            var mapperConfig =_mapper.ConfigurationProvider;

            var paginatedList = await _userRepository
                .GetAllWithPaggingSortSelectionFieldAsync<User, ProfileResponse>
                (query, mapperConfig, request.Filter.OrderBy, null, request.Filter.PageIndex, request.Filter.PageSize);

            return Result<BasePaginatedList<object>>.Success(data: paginatedList, message: "Business members retrieved successfully");

        }
    }
}
