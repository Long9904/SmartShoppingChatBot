using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Application.Features.GetMyProfile
{
    public class GetMyProfileCommandHandler : IRequestHandler<GetMyProfileCommand, Result<ProfileResponse>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;

        public GetMyProfileCommandHandler(ICurrentUserService currentUserService, IMapper mapper)
        {
            _currentUserService = currentUserService;
            _mapper = mapper;
        }
        public async Task<Result<ProfileResponse>> Handle(GetMyProfileCommand request, CancellationToken cancellationToken)
        {
            var isUser = await _currentUserService.GetUser();

            if (!isUser.IsSuccess)
            {
                return Result<ProfileResponse>.Failure(isUser.StatusCode, isUser.Message);
            }

            var response = _mapper.Map<ProfileResponse>(isUser.Data);

            return Result<ProfileResponse>.Success(response, 200, "User profile retrieved successfully.");

        }
    }
}
