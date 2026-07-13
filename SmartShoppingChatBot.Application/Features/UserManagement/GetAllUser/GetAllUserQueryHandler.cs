using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.UserManagement.GetAllUser
{
    public class GetAllUserQueryHandler : IRequestHandler<GetAllUserQuery, Result<BasePaginatedList<object>>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public GetAllUserQueryHandler(IUserRepository userRepository, IMapper mapper)
        {
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<Result<BasePaginatedList<object>>> Handle(GetAllUserQuery request, CancellationToken cancellationToken)
        {
            var query = _userRepository.AsQueryable();
            query = query.Where(x => x.UserStatus != UserStatus.DELETED);

            if (!string.IsNullOrEmpty(request.FullName))
            {
                query = query.Where(x => x.FullName.Contains(request.FullName));
            }

            if (!string.IsNullOrEmpty(request.Email))
            {
                query = query.Where(x => x.Email.Contains(request.Email));
            }

            if (request.IsEmailVerified.HasValue)
            {
                query = query.Where(x => x.IsEmailVerified == request.IsEmailVerified.Value);
            }

            if (request.Gender.HasValue)
            {
                query = query.Where(x => x.Gender == request.Gender.Value);
            }

            if (request.UserStatus.HasValue)
            {
                query = query.Where(x => x.UserStatus == request.UserStatus.Value);
            }

            var orderBy = request.OrderBy ?? "BusinessName asc, JoinedAt desc";

            var mapperConfig = _mapper.ConfigurationProvider;

            var paginatedList = await _userRepository
             .GetAllWithPaggingSortSelectionFieldAsync<User, ProfileResponse>
             (query, mapperConfig, orderBy, null, request.PageIndex, request.PageSize);

            return Result<BasePaginatedList<object>>.Success(paginatedList, 200, "Get all user successfully");
        }
    }
}
