using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.SelectBusiness
{
    public class SelectBusinessCommandHandler : IRequestHandler<SelectBusinessCommand, Result<SelectBusinessResponse>>
    {
        private readonly ITokenService _tokenService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IBusinessRepository _businessRepository;
        private readonly IUserRepository _userRepository;

        public SelectBusinessCommandHandler(
            ITokenService tokenService, 
            ICurrentUserService currentUserService, 
            IBusinessRepository businessRepository,
            IUserRepository userRepository)
        {
            _tokenService = tokenService;
            _currentUserService = currentUserService;
            _businessRepository = businessRepository;
            _userRepository = userRepository;
        }

        public async Task<Result<SelectBusinessResponse>> Handle(
            SelectBusinessCommand request, 
            CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();
            var user = await _userRepository.FindAsync(u => u.Id.ToString() == userId);


            if (user == null)
            {
                return Result<SelectBusinessResponse>.Failure(401, "Authentication Fail");
            }

            var isUserAssociatedWithBusiness = user.Businesses.Any(b => b.Id == request.BusinessId);

            if (!isUserAssociatedWithBusiness)
            {
                return Result<SelectBusinessResponse>.Failure(403, "User is not associated with the selected business.");
            }

            var business = await _businessRepository.FindAsync(b => b.Id == request.BusinessId);

            if (business == null)
            {
                return Result<SelectBusinessResponse>.Failure(404, "Business not found.");
            }

            var selectedBusiness = user.Businesses.First(b => b.Id == request.BusinessId);

            var payload = new AccessTokenPayload{
                UserId = user.Id.ToString(),
                BusinessId = business.Id.ToString(),
                Role = selectedBusiness.Role,
            };

            var accessToken = _tokenService.CreateAccessToken(payload);

            return Result<SelectBusinessResponse>.Success(new SelectBusinessResponse{
                AccessToken = accessToken
            }, 200, "Login successful");
        }
    }
}
