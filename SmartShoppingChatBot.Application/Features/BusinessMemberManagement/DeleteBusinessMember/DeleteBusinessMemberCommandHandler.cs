using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessMemberManagement.DeleteBusinessMember;

public class DeleteBusinessMemberCommandHandler : IRequestHandler<DeleteBusinessMemberCommand, Result<ProfileResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<DeleteBusinessMemberCommandHandler> _logger;
    private readonly TimeProvider _time;

    public DeleteBusinessMemberCommandHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<DeleteBusinessMemberCommandHandler> logger,
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
        DeleteBusinessMemberCommand request,
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

        member.UserStatus = UserStatus.DELETED;
        member.DeletedAt = _time.GetUtcNow();
        member.UpdatedAt = member.DeletedAt.Value;
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
            _logger.LogError(ex, "An error occurred while deleting catalog team member {MemberId}.", member.Id);
            return Result<ProfileResponse>.Failure(500, "An error occurred while deleting the catalog team member.");
        }

        var response = _mapper.Map<ProfileResponse>(member);
        return Result<ProfileResponse>.Success(response, 200, "Catalog team member deleted successfully.");
    }
}
