using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Application.Features.Auth.GetMyProfile
{
    public class GetMyProfileCommandHandler : IRequestHandler<GetMyProfileCommand, Result<ProfileResponse>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;
        private readonly IActivityLogService _activityLogService;
        public GetMyProfileCommandHandler(ICurrentUserService currentUserService, IMapper mapper, IActivityLogService activityLogService)
        {
            _currentUserService = currentUserService;
            _mapper = mapper;
            _activityLogService = activityLogService;
        }
        public async Task<Result<ProfileResponse>> Handle(GetMyProfileCommand request, CancellationToken cancellationToken)
        {
            var isUser = await _currentUserService.GetUser();

            if (!isUser.IsSuccess)
            {
                return Result<ProfileResponse>.Failure(isUser.StatusCode, isUser.Message);
            }

            var response = _mapper.Map<ProfileResponse>(isUser.Data);
            await _activityLogService.LogAsync(new ActivityLogRequest
            {
                
                Action = Domain.Enums.ActionLogEnums.View,
                ActorId = response.Id,
                TargetType = "UserProfile",
                TargetId = response.Id,
                Description = $"User {response.FullName} viewed their profile.",
                Status = Domain.Enums.StatusLogEnums.Success,
                Severity = Domain.Enums.SeverityLogEnums.Info,
            });

            return Result<ProfileResponse>.Success(response, 200, "User profile retrieved successfully.");

        }
    }
}
