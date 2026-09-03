using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ProfileManagement.UpdateProfile
{
    public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Result<ProfileResponse>>
    {

        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly TimeProvider _time;
        private readonly ILogger<UpdateProfileCommandHandler> _logger;
        private readonly IActivityLogService _activityLogService;

        public UpdateProfileCommandHandler(
            IUserRepository userRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            TimeProvider time,
            ILogger<UpdateProfileCommandHandler> logger,
            IActivityLogService activityLogService)
        {
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _time = time;
            _logger = logger;
            _activityLogService = activityLogService;
        }

        public async Task<Result<ProfileResponse>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
        {

            var userLogin = await _currentUserService.GetUser();
            
            if (userLogin.Data == null || !userLogin.IsSuccess)
            {
                return Result<ProfileResponse>.Failure(404, "User not found");
            }

            var existingUser = userLogin.Data;

            var dateTimeNow = _time.GetUtcNow();

            existingUser.FullName = request.FullName;
            existingUser.PhoneNumber = request.PhoneNumber;
            existingUser.DateOfBirth = request.DateOfBirth;
            existingUser.Gender = request.Gender;
            existingUser.UpdatedAt = dateTimeNow;
           

            await _userRepository.UpdateAsync(existingUser);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _activityLogService.LogAsync(new ActivityLogRequest
            {
                Action = ActionLogEnums.Update,
                ActorId = existingUser.Id.ToString(),
                TargetType = "UserProfile",
                TargetId = existingUser.Id.ToString(),
                Status = StatusLogEnums.Success,
                Severity = SeverityLogEnums.Info,
                Description = $"User {existingUser.FullName} updated their profile successfully.",
            });

            var profileResponse = _mapper.Map<ProfileResponse>(existingUser);

            return Result<ProfileResponse>.Success(profileResponse, 200, "User updated successfully");
        }
    }
}
