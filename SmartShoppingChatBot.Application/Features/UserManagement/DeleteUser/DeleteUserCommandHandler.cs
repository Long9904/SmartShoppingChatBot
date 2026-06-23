using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.UserManagement.DeleteUser
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result<ProfileResponse>>
    {
        private readonly IUserRepository _userRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _time;
        private readonly IMapper _mapper;
        private readonly ILogger<DeleteUserCommandHandler> _logger;

        public DeleteUserCommandHandler(
            IUserRepository userRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork,
            TimeProvider time,
            IMapper mapper,
            ILogger<DeleteUserCommandHandler> logger)
        {
            _userRepository = userRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _time = time;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<Result<ProfileResponse>> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            var validId = ObjectId.TryParse(request.UserId, out var userId);

            if (!validId)
            {
                return Result<ProfileResponse>.Failure(400, "Invalid user ID format.");
            }

            var user = await _userRepository.FindAsync(u => u.Id == userId);
            if (user == null) return Result<ProfileResponse>.Failure(404, "User not found.");

            var dateTimeNow = _time.GetUtcNow();
            var userLoggedIn = await _currentUserService.GetUser();

            if (!userLoggedIn.IsSuccess)
            {
                return Result<ProfileResponse>.Failure(userLoggedIn.StatusCode, userLoggedIn.Message);
            }

            user.UserStatus = UserStatus.DELETED;
            user.DeletedAt = dateTimeNow;
            user.UpdatedAt = dateTimeNow;
            user.UpdatedBy = new UserEmbedded
            {
                Id = userLoggedIn.Data.Id,
                Name = userLoggedIn.Data.FullName,
            };

            await _userRepository.UpdateAsync(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User with ID {UserId} has been marked as deleted by user {UpdatedById}.", user.Id, userLoggedIn.Data.Id);

            var response = _mapper.Map<ProfileResponse>(user);
            return Result<ProfileResponse>.Success(response, 204, "User deleted successfully.");
        }
    }
}
