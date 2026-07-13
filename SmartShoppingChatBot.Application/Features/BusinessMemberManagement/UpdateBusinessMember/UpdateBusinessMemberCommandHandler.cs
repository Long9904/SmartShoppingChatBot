using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessMemberManagement.UpdateBusinessMember;

public class UpdateBusinessMemberCommandHandler : IRequestHandler<UpdateBusinessMemberCommand, Result<ProfileResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateBusinessMemberCommandHandler> _logger;
    private readonly TimeProvider _time;

    public UpdateBusinessMemberCommandHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UpdateBusinessMemberCommandHandler> logger,
        TimeProvider time)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _time = time;
    }

    public async Task<Result<ProfileResponse>> Handle(
        UpdateBusinessMemberCommand request,
        CancellationToken cancellationToken)
    {
        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess)
        {
            return Result<ProfileResponse>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors);
        }

        var businessId = businessResult.Data!.Id;
        var member = await _userRepository.FindAsync(user =>
            user.Id == request.MemberId &&
            user.Business.Id == businessId &&
            user.Business.Role == RoleEnums.CATALOG_TEAM &&
            user.UserStatus != UserStatus.DELETED);

        if (member == null)
        {
            return Result<ProfileResponse>.Failure(404, "Catalog team member not found.");
        }

        var currentUserResult = await _currentUserService.GetUser();
        if (!currentUserResult.IsSuccess)
        {
            return Result<ProfileResponse>.Failure(
                currentUserResult.StatusCode,
                currentUserResult.Message,
                currentUserResult.Errors);
        }

        member.FullName = request.FullName.Trim();
        member.PhoneNumber = request.PhoneNumber?.Trim();
        member.DateOfBirth = request.DateOfBirth;
        member.Gender = request.Gender;
        member.UpdatedAt = _time.GetUtcNow();
        member.Email = request.Email.Trim();
        member.UpdatedBy = new UserEmbedded
        {
            Id = currentUserResult.Data!.Id,
            Name = currentUserResult.Data.FullName
        };

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            await _userRepository.UpdateAsync(member);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollBackAsync(cancellationToken);
            _logger.LogError(ex, "An error occurred while updating catalog team member {MemberId}.", member.Id);
            return Result<ProfileResponse>.Failure(500, "An error occurred while updating the catalog team member.");
        }

        var response = _mapper.Map<ProfileResponse>(member);
        return Result<ProfileResponse>.Success(response, 200, "Catalog team member updated successfully.");
    }
}
