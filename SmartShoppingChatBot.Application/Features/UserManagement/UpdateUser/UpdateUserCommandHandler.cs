using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.UserManagement.UpdateUser
{
    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result<ProfileResponse>>
    {

        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly TimeProvider _time;
        private readonly ILogger<UpdateUserCommandHandler> _logger;

        public UpdateUserCommandHandler(
            IUserRepository userRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork,
            IMapper mapper,
            TimeProvider time,
            ILogger<UpdateUserCommandHandler> logger)
        {
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _time = time;
            _logger = logger;
        }

        public async Task<Result<ProfileResponse>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {

            var existingUser = await _userRepository.FindAsync(x => x.Id == request.UserId);

            if (existingUser == null)
            {
                return Result<ProfileResponse>.Failure(404, "User not found");
            }

            var currentUserResult = await _currentUserService.GetUser();
            if (!currentUserResult.IsSuccess)
            {
                return Result<ProfileResponse>.Failure(
                    currentUserResult.StatusCode,
                    currentUserResult.Message,
                    currentUserResult.Errors);
            }

            var dateTimeNow = _time.GetUtcNow();

            existingUser.FullName = request.FullName;
            existingUser.PhoneNumber = request.PhoneNumber;
            existingUser.DateOfBirth = request.DateOfBirth;
            existingUser.Gender = request.Gender;
            existingUser.UpdatedAt = dateTimeNow;
            existingUser.UpdatedBy = new()
            {
                Id = currentUserResult.Data.Id,
                Name = currentUserResult.Data.FullName
            };

            await _userRepository.UpdateAsync(existingUser);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var profileResponse = _mapper.Map<ProfileResponse>(existingUser);

            return Result<ProfileResponse>.Success(profileResponse, 200, "User updated successfully");
        }
    }
}
