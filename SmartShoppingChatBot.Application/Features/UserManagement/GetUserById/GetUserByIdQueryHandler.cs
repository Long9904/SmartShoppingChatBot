using AutoMapper;
using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.UserManagement.GetUserById
{
    internal class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, Result<ProfileResponse>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;

        public GetUserByIdQueryHandler(IUserRepository userRepository, IMapper mapper)
        {
            _userRepository = userRepository;
            _mapper = mapper;
        }

        public async Task<Result<ProfileResponse>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.FindAsync(u => u.Id == ObjectId.Parse(request.UserId));

            if (user == null)
            {
                return Result<ProfileResponse>.Failure(statusCode: 404, message: "User not found");
            }

            var profileResponse = _mapper.Map<ProfileResponse>(user);

            return Result<ProfileResponse>.Success(profileResponse);
        }
    }
}
